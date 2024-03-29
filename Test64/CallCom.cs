﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace UnitTestProject1
{

    [ComVisible(true)]
    [Guid("2DCA6610-6BB6-44A1-AD82-5175FFDDC955")] // {2DCA6610-6BB6-44A1-AD82-5175FFDDC955}
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICreateDump
    {
        int CreateDump(int PidToDump, int UseSnapshot, string pathDumpFileName);
        int CreateDumpFromPSSSnapshot(int PidToDump, IntPtr hSnapshot, string pathDumpFileName);


    }
    public class CallCom
    {
        public ICreateDump  GetInterfaceICreateDump()
        {
            var guidComClass = new Guid("E3E00445-C0EA-4AB2-BF1C-309358F7EC3A");
            var dllname = @"C:\Users\calvinh\source\repos\CreateDump\x64\Debug\CppCreateDumpLib.dll";
            if (!File.Exists(dllname))
            {
                throw new FileNotFoundException(dllname);
            }
            var hr = CoCreateFromFile(dllname, guidComClass, typeof(ICreateDump).GUID, out var pObject);
            if (hr != 0)
            {
                throw new InvalidOperationException($"Couldn't create obj {hr:x8} {Marshal.GetExceptionForHR(hr)}");
            }
            var iCreateDump = (ICreateDump)Marshal.GetTypedObjectForIUnknown(pObject, typeof(ICreateDump));
            return iCreateDump;
        }

        // see https://github.com/calvinhsia/HwndHost/blob/320363cf1f687a1e3074ad7aac7cd05a3f7e49d1/AreaFill/AreaFillWindow.xaml.cs#L1049
        static class HResult
        {
            public const int S_OK = 0;
            public const int S_FALSE = 1;
            public const int E_FAIL = unchecked((int)0x80004005);
        }
        internal delegate int DllGetClassObject(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid ClassId,
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            out IntPtr ppvObject);

        delegate int CanUnloadNowRoutine();
        CanUnloadNowRoutine _deldllCanUnloadNow;

        IntPtr _hModule = IntPtr.Zero;
        /// <summary>Creates com object with the given clsid in the specified file</summary>
        /// <param name="fnameComClass">The path of the module</param>
        /// <param name="clsidOfComObj">The CLSID of the com object</param>
        /// <param name="riid">The IID of the interface requested</param>
        /// <param name="pvObject">The interface pointer. Upon failure pvObject is IntPtr.Zero</param>
        /// <returns>An HRESULT</returns>
        [HandleProcessCorruptedStateExceptions]
        internal int CoCreateFromFile(string fnameComClass, Guid clsidOfComObj, Guid riid, out IntPtr pvObject)
        {
            pvObject = IntPtr.Zero;
            int hr = HResult.E_FAIL;
            try
            {
                _hModule = LoadLibrary(fnameComClass);
                if (_hModule != IntPtr.Zero)
                {
                    IntPtr optrDllGetClassObject = GetProcAddress(_hModule, "DllGetClassObject");
                    if (optrDllGetClassObject != IntPtr.Zero)
                    {
                        var delDllGetClassObject = Marshal.GetDelegateForFunctionPointer<DllGetClassObject>(optrDllGetClassObject);
                        var optrDllCanUnloadNow = GetProcAddress(_hModule, "DllCanUnloadNow");
                        _deldllCanUnloadNow = Marshal.GetDelegateForFunctionPointer<CanUnloadNowRoutine>(optrDllCanUnloadNow);

                        IntPtr pClassFactory = IntPtr.Zero;
                        Guid iidIUnknown = new Guid(IUnknownGuid);
                        hr = delDllGetClassObject(clsidOfComObj, iidIUnknown, out pClassFactory);
                        if (hr == HResult.S_OK)
                        {
                            var classFactory = (IClassFactory)Marshal.GetTypedObjectForIUnknown(pClassFactory, typeof(IClassFactory));
                            hr = classFactory.CreateInstance(IntPtr.Zero, ref riid, out pvObject);
                            Marshal.ReleaseComObject(classFactory);
                            Marshal.Release(pClassFactory);
                        }
                    }
                    else
                    {
                        hr = Marshal.GetHRForLastWin32Error();
                        throw new InvalidOperationException( $"Unable to find DllGetClassObject: {hr} {Marshal.GetExceptionForHR(hr)}");
                    }
                }
                else
                {
                    hr = Marshal.GetHRForLastWin32Error();
                    throw new InvalidOperationException($"Unable to load {fnameComClass}: {hr} {Marshal.GetExceptionForHR(hr)}");
                }
            }
            catch (Exception ex)
            {
                var x = ex.ToString(); // HandleProcessCorruptedStateExceptions
                throw new InvalidOperationException(x);

            }
            return hr;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr LoadLibrary(string dllName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int FreeLibrary(IntPtr handle);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procname);


        const string IUnknownGuid = "00000001-0000-0000-C000-000000000046";
        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid(IUnknownGuid)]
        private interface IClassFactory
        {
            [PreserveSig]
            int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);
            int LockServer(int fLock);
        }
    }

}

﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace CreateAsm
{
    /// <summary>
    /// We want to create an assembly that will be loaded in an exe (perhaps 64 bit) that will load and call a target method (could be static or non-static)
    /// Taking a process dump of a 64 bit process from a 32 bit process doesn't work. Even from 32 bit task manager.
    /// This code emits an Asm that can be made into a 64 bit executable
    /// The goal is to call a static method in 32 bit PerfWatson in a static class MemoryDumpHelper with the signature:
    ///           public static void CollectDump(int procid, string pathOutput, bool FullHeap)
    /// The generated asm can be saved as an exe on disk, then started from 32 bit code. 
    ///  A little wrinkle: in order to enumerate the types in the DLL, the Appdomain AsemblyResolver needs to find the dependencies
    /// The 64 bit process will then load the 32 bit PW IL (using the assembly resolver, then invoke the method via reflection)
    /// the parameters are pased to the 64 bit exe on the commandline.
    /// This code logs output to the output file (which is the dump file when called with logging false)
    /// The code generates a static Main (string[] args) method.
    ///  see https://github.com/calvinhsia/CreateDump
    /// </summary>
    public class AssemblyCreator
    {
        public Type CreateAssembly(
                string targPEFile,
                PortableExecutableKinds portableExecutableKinds,
                ImageFileMachine imageFileMachine,
                string AdditionalAssemblyPath,
                bool logOutput = false,
                bool CauseException = false
            )
        {
            var typeName = Path.GetFileNameWithoutExtension(targPEFile);
            var aName = new AssemblyName(typeName);
            // the Appdomain DefineDynamicAssembly has an overload for Dir
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
                aName,
                AssemblyBuilderAccess.RunAndSave,
                dir: Path.GetDirectoryName(targPEFile));
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(aName.Name + ".exe");
            var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public);
            var statTarg32bitDll = typeBuilder.DefineField("targ32bitDll", typeof(string), FieldAttributes.Static);
            var statStringBuilder = typeBuilder.DefineField("_StringBuilder", typeof(StringBuilder), FieldAttributes.Static);
            var statLogOutputFile = typeBuilder.DefineField("_logOutputFile", typeof(string), FieldAttributes.Static);
            MethodBuilder AsmResolveMethodBuilder = null;
            if (!string.IsNullOrEmpty(AdditionalAssemblyPath))
            {
                AsmResolveMethodBuilder = typeBuilder.DefineMethod(
                    "CurrentDomain_AssemblyResolve",
                    MethodAttributes.Static,
                    returnType: typeof(Assembly),
                    parameterTypes: new Type[] { typeof(object), typeof(ResolveEventArgs) }
                    );
                {
                    var il = AsmResolveMethodBuilder.GetILGenerator();
                    il.DeclareLocal(typeof(Assembly));//0 // retvalue
                    il.DeclareLocal(typeof(string)); //1 var privAsmDir = Path.Combine(Path.GetDirectoryName(targ32bitDll), "PrivateAssemblies");
                    il.DeclareLocal(typeof(string)); //2 requestName =Microsoft.VisualStudio.Telemetry

                    if (logOutput)
                    {
                        il.Emit(OpCodes.Ldsfld, statStringBuilder);
                        il.Emit(OpCodes.Ldstr, "InResolveMethod");
                        il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                        il.Emit(OpCodes.Pop);
                    }

                    //var privAsmDir = Path.Combine(Path.GetDirectoryName(targ32bitDll), "PrivateAssemblies");
                    il.Emit(OpCodes.Ldsfld, statTarg32bitDll);
                    il.Emit(OpCodes.Call, typeof(Path).GetMethod("GetDirectoryName", new Type[] { typeof(string) }));
                    il.Emit(OpCodes.Ldstr, AdditionalAssemblyPath);
                    il.Emit(OpCodes.Call, typeof(Path).GetMethod("Combine", new Type[] { typeof(string), typeof(string) }));
                    il.Emit(OpCodes.Stloc_1);

                    if (logOutput)
                    {
                        il.Emit(OpCodes.Ldsfld, statStringBuilder);
                        il.Emit(OpCodes.Ldloc_1);
                        il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                        il.Emit(OpCodes.Pop);
                    }

                    //var requestName = args.Name.Substring(0, args.Name.IndexOf(",")); // Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Callvirt, typeof(ResolveEventArgs).GetProperty("Name").GetMethod); // Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Callvirt, typeof(ResolveEventArgs).GetProperty("Name").GetMethod); // Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
                    il.Emit(OpCodes.Ldstr, ",");
                    il.Emit(OpCodes.Callvirt, typeof(string).GetMethod("IndexOf", new Type[] { typeof(string) }));
                    il.Emit(OpCodes.Callvirt, typeof(string).GetMethod("Substring", new Type[] { typeof(Int32), typeof(Int32) }));
                    il.Emit(OpCodes.Stloc_2); // Microsoft.VisualStudio.Telemetry

                    if (logOutput)
                    {
                        il.Emit(OpCodes.Ldsfld, statStringBuilder);
                        il.Emit(OpCodes.Ldloc_2);
                        il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                        il.Emit(OpCodes.Pop);
                    }

                    //asm = Assembly.LoadFrom(Path.Combine(privAsmDir, $"{requestName}.dll"));
                    il.Emit(OpCodes.Ldloc_1);
                    il.Emit(OpCodes.Ldloc_2);
                    il.Emit(OpCodes.Ldstr, ".dll");
                    il.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", new Type[] { typeof(string), typeof(string) }));
                    il.Emit(OpCodes.Call, typeof(Path).GetMethod("Combine", new Type[] { typeof(string), typeof(string) }));
                    il.Emit(OpCodes.Call, typeof(Assembly).GetMethod("LoadFrom", new Type[] { typeof(string) }));

                    il.Emit(OpCodes.Ret);
                }
            }
            var mainMethodBuilder = typeBuilder.DefineMethod(
                "Main",
                MethodAttributes.Public | MethodAttributes.Static,
                returnType: null,
                parameterTypes: new Type[] { typeof(string[]) });
            {
                var il = mainMethodBuilder.GetILGenerator();
                var labEnd = il.DefineLabel();
                il.DeclareLocal(typeof(string));//0
                il.DeclareLocal(typeof(DateTime));//1
                il.DeclareLocal(typeof(Assembly)); //2 targ32bitasm
                il.DeclareLocal(typeof(Type[])); //3 
                il.DeclareLocal(typeof(Int32)); // 4
                il.DeclareLocal(typeof(Type)); // 5 // as we iterate types
                il.DeclareLocal(typeof(string)); // 6 // string typename in loop
                il.DeclareLocal(typeof(MethodInfo)); // 7 method
                il.DeclareLocal(typeof(object));// 8 Activator.CreateInstance
                il.DeclareLocal(typeof(object[])); // 9 argsToPass
                il.DeclareLocal(typeof(Int32));//10 pidAsString

                il.BeginExceptionBlock();
                {
                    il.Emit(OpCodes.Newobj, typeof(StringBuilder).GetConstructor(new Type[0]));
                    il.Emit(OpCodes.Stsfld, statStringBuilder);
                    il.Emit(OpCodes.Ldc_I4_5); //Environment.SpecialFolder.MyDocuments
                    il.Emit(OpCodes.Call, typeof(Environment).GetMethod("GetFolderPath", new Type[] { typeof(Environment.SpecialFolder) }));
                    il.Emit(OpCodes.Ldstr, "MyTestAsm.log");
                    il.Emit(OpCodes.Call, typeof(Path).GetMethod("Combine", new Type[] { typeof(string), typeof(string) }));
                    il.Emit(OpCodes.Stsfld, statLogOutputFile);

                    il.Emit(OpCodes.Ldsfld, statStringBuilder);
                    il.Emit(OpCodes.Ldstr, "InMyTestAsm!!!");
                    il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));

                    il.Emit(OpCodes.Call, typeof(Environment).GetProperty("CommandLine").GetMethod);
                    il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                    il.Emit(OpCodes.Pop);

                    if (CauseException)
                    {
                        il.Emit(OpCodes.Ldnull);
                        il.Emit(OpCodes.Callvirt, typeof(object).GetMethod("ToString", new Type[0]));
                    }

                    //targ32bitDll = args[0];
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Ldelem_Ref);
                    il.Emit(OpCodes.Stsfld, statTarg32bitDll);
                    if (AsmResolveMethodBuilder != null)
                    {
                        //AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                        il.Emit(OpCodes.Call, typeof(AppDomain).GetProperty("CurrentDomain").GetMethod);
                        il.Emit(OpCodes.Ldnull);
                        il.Emit(OpCodes.Ldftn, AsmResolveMethodBuilder);
                        il.Emit(OpCodes.Newobj, typeof(ResolveEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
                        il.Emit(OpCodes.Callvirt, typeof(AppDomain).GetEvent("AssemblyResolve").GetAddMethod());

                        if (logOutput)
                        {
                            il.Emit(OpCodes.Ldsfld, statStringBuilder);
                            il.Emit(OpCodes.Ldstr, "Asm ResolveEvents events subscribed");
                            il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                            il.Emit(OpCodes.Pop);
                        }
                    }


                    //var asmprog32 = Assembly.LoadFrom(args[0]);
                    il.Emit(OpCodes.Ldsfld, statTarg32bitDll);
                    il.Emit(OpCodes.Call, typeof(Assembly).GetMethod("LoadFrom", new Type[] { typeof(string) }));
                    il.Emit(OpCodes.Stloc_2);

                    //foreach (var type in asmprog32.GetExportedTypes())
                    il.Emit(OpCodes.Ldloc_2);
                    il.Emit(OpCodes.Callvirt, typeof(Assembly).GetMethod("GetTypes"));
                    il.Emit(OpCodes.Stloc_3); // type[]

                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Stloc, 4); // loop index
                    var labIncLoop = il.DefineLabel();
                    var labBreakLoop = il.DefineLabel();
                    il.Emit(OpCodes.Br, labIncLoop);
                    {
                        var labStartLoop = il.DefineLabel();
                        il.MarkLabel(labStartLoop);

                        il.Emit(OpCodes.Ldloc_3); // type[]
                        il.Emit(OpCodes.Ldloc, 4);// loop index
                        il.Emit(OpCodes.Ldelem_Ref);
                        il.Emit(OpCodes.Stloc, 5);
                        il.Emit(OpCodes.Ldloc, 5);
                        il.Emit(OpCodes.Callvirt, typeof(MemberInfo).GetProperty("Name").GetMethod);
                        il.Emit(OpCodes.Stloc, 6);

                        if (logOutput)
                        {
                            il.Emit(OpCodes.Ldsfld, statStringBuilder);
                            il.Emit(OpCodes.Ldloc, 6);
                            il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                            il.Emit(OpCodes.Pop);
                        }

                        //if (type.Name == args[1])
                        var labNotOurType = il.DefineLabel();
                        il.Emit(OpCodes.Ldloc, 6);
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldc_I4_1);
                        il.Emit(OpCodes.Ldelem_Ref);

                        il.Emit(OpCodes.Call, typeof(string).GetMethod("op_Equality", new Type[] { typeof(string), typeof(string) }));
                        il.Emit(OpCodes.Brfalse, labNotOurType);
                        {
                            if (logOutput)
                            {
                                il.Emit(OpCodes.Ldsfld, statStringBuilder);
                                il.Emit(OpCodes.Ldstr, "GotOurType");
                                il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                                il.Emit(OpCodes.Pop);
                            }

                            //var methCollectDump = type.GetMethod(args[2]);
                            il.Emit(OpCodes.Ldloc, 5);
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Ldc_I4_2);
                            il.Emit(OpCodes.Ldelem_Ref);
                            il.Emit(OpCodes.Ldc_I4, 24); // static ==8, nonpublic == 32, public == 16
                            il.Emit(OpCodes.Callvirt, typeof(Type).GetMethod("GetMethod", new Type[] { typeof(string), typeof(BindingFlags) }));
                            il.Emit(OpCodes.Stloc, 7);

                            if (logOutput)
                            {
                                il.Emit(OpCodes.Ldsfld, statStringBuilder);
                                il.Emit(OpCodes.Ldstr, "GotOurMethod");
                                il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                                il.Emit(OpCodes.Pop);
                            }

                            //var pidAsString = int.Parse(args[3]);
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Ldc_I4_3); // args subscript 3
                            il.Emit(OpCodes.Ldelem_Ref);
                            il.Emit(OpCodes.Call, typeof(Int32).GetMethod("Parse", new Type[] { typeof(string) }));
                            il.Emit(OpCodes.Stloc, 10);

                            //var argsToPass = new object[] { pidAsString, args[4], true };
                            il.Emit(OpCodes.Ldc_I4_3); // size of array
                            il.Emit(OpCodes.Newarr, typeof(Object));
                            il.Emit(OpCodes.Stloc, 9);

                            il.Emit(OpCodes.Ldloc, 9);
                            il.Emit(OpCodes.Ldc_I4_0); // array elem 0
                            il.Emit(OpCodes.Ldloc, 10);
                            il.Emit(OpCodes.Box, typeof(Int32));
                            il.Emit(OpCodes.Stelem_Ref);

                            il.Emit(OpCodes.Ldloc, 9);
                            il.Emit(OpCodes.Ldc_I4_1); // elem 1
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Ldc_I4_4);
                            il.Emit(OpCodes.Ldelem_Ref);
                            il.Emit(OpCodes.Stelem_Ref);

                            il.Emit(OpCodes.Ldloc, 9);
                            il.Emit(OpCodes.Ldc_I4_2); // elem[2]
                            il.Emit(OpCodes.Ldc_I4_1); // true
                            il.Emit(OpCodes.Box, typeof(Boolean));
                            il.Emit(OpCodes.Stelem_Ref);

                            if (logOutput)
                            {
                                il.Emit(OpCodes.Ldsfld, statStringBuilder);
                                il.Emit(OpCodes.Ldstr, "CreatedParms");
                                il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                                il.Emit(OpCodes.Pop);

                                for (int i = 0; i < 3; i++)
                                {
                                    il.Emit(OpCodes.Ldsfld, statStringBuilder);
                                    il.Emit(OpCodes.Ldloc, 9);
                                    il.Emit(OpCodes.Ldc_I4, i);
                                    il.Emit(OpCodes.Ldelem_Ref);
                                    il.Emit(OpCodes.Callvirt, typeof(object).GetMethod("ToString", new Type[0]));
                                    il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                                    il.Emit(OpCodes.Pop);
                                }

                                il.Emit(OpCodes.Ldsfld, statStringBuilder);
                                il.Emit(OpCodes.Ldloc, 7); // Void CollectDump(Int32, System.String, Boolean)
                                il.Emit(OpCodes.Callvirt, typeof(object).GetMethod("ToString", new Type[0]));
                                il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                                il.Emit(OpCodes.Pop);

                                il.Emit(OpCodes.Ldsfld, statStringBuilder);
                                il.Emit(OpCodes.Ldloc, 9);
                                il.Emit(OpCodes.Callvirt, typeof(object).GetMethod("ToString", new Type[0]));
                                il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                                il.Emit(OpCodes.Pop);
                            }
                            if (logOutput)
                            {
                                // before we invoke, we need to flush our log
                                il.Emit(OpCodes.Ldsfld, statLogOutputFile);
                                il.Emit(OpCodes.Ldsfld, statStringBuilder);
                                il.Emit(OpCodes.Call, typeof(StringBuilder).GetMethod("ToString", new Type[0]));
                                il.Emit(OpCodes.Call, typeof(File).GetMethod("AppendAllText", new Type[] { typeof(string), typeof(string) }));

                                il.Emit(OpCodes.Ldsfld, statStringBuilder);
                                il.Emit(OpCodes.Call, typeof(StringBuilder).GetMethod("Clear"));
                                il.Emit(OpCodes.Pop);
                            }

                            //methCollectDump.Invoke(memdumpHelper, argsToPass);
                            il.Emit(OpCodes.Ldloc, 7); // method
                            il.Emit(OpCodes.Ldnull); // instance
                            il.Emit(OpCodes.Ldloc, 9); // args
                            il.Emit(OpCodes.Callvirt, typeof(MethodBase).GetMethod("Invoke", new Type[] { typeof(object), typeof(object[]) }));
                            il.Emit(OpCodes.Pop);

                            if (logOutput)
                            {
                                il.Emit(OpCodes.Ldsfld, statStringBuilder);
                                il.Emit(OpCodes.Ldstr, "back from call");
                                il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                                il.Emit(OpCodes.Pop);
                            }

                            //break;
                            il.Emit(OpCodes.Br, labBreakLoop);
                        }
                        il.MarkLabel(labNotOurType);

                        // increment count
                        il.Emit(OpCodes.Ldloc, 4);
                        il.Emit(OpCodes.Ldc_I4_1);
                        il.Emit(OpCodes.Add);
                        il.Emit(OpCodes.Stloc, 4);

                        il.MarkLabel(labIncLoop);
                        il.Emit(OpCodes.Ldloc, 4);
                        il.Emit(OpCodes.Ldloc_3);
                        il.Emit(OpCodes.Ldlen);
                        il.Emit(OpCodes.Conv_I4);
                        il.Emit(OpCodes.Blt, labStartLoop);
                    }
                    il.MarkLabel(labBreakLoop);

                }

                il.BeginCatchBlock(typeof(Exception)); // exception is on eval stack
                {
                    il.Emit(OpCodes.Call, typeof(Exception).GetMethod("ToString", new Type[0]));
                    il.Emit(OpCodes.Stloc_0);

                    if (logOutput)
                    {
                        il.Emit(OpCodes.Ldsfld, statStringBuilder);
                        il.Emit(OpCodes.Call, typeof(DateTime).GetProperty("Now").GetMethod);
                        il.Emit(OpCodes.Stloc_1);
                        il.Emit(OpCodes.Ldloca_S, 1);
                        il.Emit(OpCodes.Callvirt, typeof(DateTime).GetMethod("ToString", new Type[0]));
                        il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));

                        il.Emit(OpCodes.Ldstr, "Exception thrown");
                        il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                        il.Emit(OpCodes.Ldloc_0);
                        il.Emit(OpCodes.Call, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                        il.Emit(OpCodes.Pop);
                    }
                }
                il.EndExceptionBlock();

                if (AsmResolveMethodBuilder != null)
                {
                    //AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
                    il.Emit(OpCodes.Call, typeof(AppDomain).GetProperty("CurrentDomain").GetMethod);
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Ldftn, AsmResolveMethodBuilder);
                    il.Emit(OpCodes.Newobj, typeof(ResolveEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
                    il.Emit(OpCodes.Callvirt, typeof(AppDomain).GetEvent("AssemblyResolve").GetRemoveMethod());

                    il.Emit(OpCodes.Ldsfld, statStringBuilder);
                    il.Emit(OpCodes.Ldstr, "Asm ResolveEvents events Unsubscribed");
                    il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                    il.Emit(OpCodes.Pop);

                }
                if (logOutput)
                {
                    il.Emit(OpCodes.Ldsfld, statLogOutputFile);
                    il.Emit(OpCodes.Ldsfld, statStringBuilder);
                    il.Emit(OpCodes.Call, typeof(StringBuilder).GetMethod("ToString", new Type[0]));
                    il.Emit(OpCodes.Call, typeof(File).GetMethod("AppendAllText", new Type[] { typeof(string), typeof(string) }));
                }
                il.MarkLabel(labEnd);
                il.Emit(OpCodes.Ret);
            }
            var type = typeBuilder.CreateType();

            assemblyBuilder.SetEntryPoint(mainMethodBuilder, PEFileKinds.WindowApplication);
            assemblyBuilder.Save($"{typeName}.exe", portableExecutableKinds, imageFileMachine);
            return type;
        }
    }

    public static class TargetStaticClass
    {
        public static string outputFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MyTestAsm.log");
        static StringBuilder sb = new StringBuilder();
        public static void MyStaticMethodWithNoParams()
        {
            sb.AppendLine($"{DateTime.Now}");
            sb.AppendLine($"Here I am in {nameof(TargetStaticClass)} {nameof(MyStaticMethodWithNoParams)}");
            sb.AppendLine($"Assembly = {Assembly.GetExecutingAssembly().Location}");
            sb.AppendLine($"IntPtr.Size == {IntPtr.Size}");
            File.AppendAllText(outputFile, sb.ToString());
        }
        public static void MyStaticMethodWith3Param(int param1, string param2, bool param3)
        {
            sb.AppendLine($"{DateTime.Now}");
            sb.AppendLine($"Here I am in {nameof(TargetStaticClass)} {nameof(MyStaticMethodWith3Param)}");
            sb.AppendLine($"Assembly = {Assembly.GetExecutingAssembly().Location}");
            sb.AppendLine($"parm1== {param1} parm2 = {param2} parm3={param3}");
            sb.AppendLine($"IntPtr.Size == {IntPtr.Size}");
            File.AppendAllText(outputFile, sb.ToString());
        }
    }

    internal class TargetClass
    {
        //        string outputFile = @"C:\Users\calvinh\Documents\MyTestAsm.log";
        string outputFile => TargetStaticClass.outputFile;
        StringBuilder sb = new StringBuilder();
        private void MyPrivateMethodWith1Param(string param1)
        {
            sb.AppendLine($"{DateTime.Now}");
            sb.AppendLine($"Here I am in {nameof(TargetClass)} {nameof(MyPrivateMethodWith1Param)}");
            sb.AppendLine($"Assembly = {Assembly.GetExecutingAssembly().Location}");
            sb.AppendLine($"IntPtr.Size == {IntPtr.Size}");
            File.AppendAllText(outputFile, sb.ToString());
        }
    }
}
using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace UnitTestProject1
{
    internal class Create64Bit
    {
        private readonly string _targ64PEFile;
        private readonly string _TypeName;

        public Create64Bit(string targ64PEFile, string TypeName)
        {
            _targ64PEFile = targ64PEFile;
            _TypeName = TypeName;
        }

        public void Create64BitExeUsingEmit()
        {
            var aName = new AssemblyName(Path.GetFileNameWithoutExtension(_targ64PEFile));
            // the Appdomain DefineDynamicAssembly has an overload for Dir
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(aName, AssemblyBuilderAccess.Save, dir: Path.GetDirectoryName(_targ64PEFile));
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(aName.Name + ".exe");
            var typeBuilder = moduleBuilder.DefineType(_TypeName, TypeAttributes.Public);
            var statTarg32bitDll = typeBuilder.DefineField("targ32bitDll", typeof(string), FieldAttributes.Static);
            var statStringBuilder = typeBuilder.DefineField("_StringBuilder", typeof(StringBuilder), FieldAttributes.Static);
            var AsmResolveMethodBuilder = typeBuilder.DefineMethod(
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

                il.Emit(OpCodes.Ldsfld, statStringBuilder);
                il.Emit(OpCodes.Ldstr, "InResolveMethod");
                il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));

                il.Emit(OpCodes.Ldsfld, statTarg32bitDll);
                il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));

                //var privAsmDir = Path.Combine(Path.GetDirectoryName(targ32bitDll), "PrivateAssemblies");
                il.Emit(OpCodes.Ldsfld, statTarg32bitDll);
                il.Emit(OpCodes.Call, typeof(Path).GetMethod("GetDirectoryName", new Type[] { typeof(string) }));
                il.Emit(OpCodes.Ldstr, "PrivateAssemblies");
                il.Emit(OpCodes.Call, typeof(Path).GetMethod("Combine", new Type[] { typeof(string), typeof(string) }));
                il.Emit(OpCodes.Stloc_1);
                il.Emit(OpCodes.Ldloc_1);

                il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));

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
                il.Emit(OpCodes.Ldloc_2);
                il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                il.Emit(OpCodes.Pop);

                il.Emit(OpCodes.Ldloc_2);
                il.Emit(OpCodes.Ldstr, "Microsoft.VisualStudio.Telemetry");
                il.Emit(OpCodes.Call, typeof(string).GetMethod("op_Equality", new Type[] { typeof(string), typeof(string) }));
                var labIsNotVSTelem = il.DefineLabel();
                var labDone = il.DefineLabel();
                il.Emit(OpCodes.Brfalse_S, labIsNotVSTelem);
                {
                    il.Emit(OpCodes.Ldsfld, statStringBuilder);
                    il.Emit(OpCodes.Ldstr, "IsVsTelem");
                    il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                    il.Emit(OpCodes.Pop);

                    il.Emit(OpCodes.Ldloc_1);
                    il.Emit(OpCodes.Ldstr, "Microsoft.VisualStudio.Telemetry.Dll");
                    il.Emit(OpCodes.Call, typeof(Path).GetMethod("Combine", new Type[] { typeof(string), typeof(string) }));
                    il.Emit(OpCodes.Call, typeof(Assembly).GetMethod("LoadFrom", new Type[] { typeof(string) }));
                    il.Emit(OpCodes.Stloc_0);
                    il.Emit(OpCodes.Br_S, labDone);
                }
                il.MarkLabel(labIsNotVSTelem);
                il.Emit(OpCodes.Ldloc_2);
                il.Emit(OpCodes.Ldstr, "Newtonsoft.Json");
                il.Emit(OpCodes.Call, typeof(string).GetMethod("op_Equality", new Type[] { typeof(string), typeof(string) }));
                il.Emit(OpCodes.Brfalse_S, labDone);
                {
                    il.Emit(OpCodes.Ldsfld, statStringBuilder);
                    il.Emit(OpCodes.Ldstr, "IsJson");
                    il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                    il.Emit(OpCodes.Pop);

                    il.Emit(OpCodes.Ldloc_1);
                    il.Emit(OpCodes.Ldstr, "Newtonsoft.Json.Dll");
                    il.Emit(OpCodes.Call, typeof(Path).GetMethod("Combine", new Type[] { typeof(string), typeof(string) }));
                    il.Emit(OpCodes.Call, typeof(Assembly).GetMethod("LoadFrom", new Type[] { typeof(string) }));
                    il.Emit(OpCodes.Stloc_0);
                    il.Emit(OpCodes.Br_S, labDone);
                }
                il.MarkLabel(labDone);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ret);
            }
            var mainMethodBuilder = typeBuilder.DefineMethod(
                "Main",
                MethodAttributes.Public | MethodAttributes.Static,
                returnType: null,
                parameterTypes: new Type[] { typeof(string[]) });
            {
                var il = mainMethodBuilder.GetILGenerator();

                il.DeclareLocal(typeof(string));//0
                il.DeclareLocal(typeof(DateTime));//1
                il.DeclareLocal(typeof(Assembly)); //2 targ32bitasm
                il.DeclareLocal(typeof(Type[])); //3 
                il.DeclareLocal(typeof(Int32)); // 4

                il.BeginExceptionBlock();
                {
                    il.Emit(OpCodes.Newobj, typeof(StringBuilder).GetConstructor(new Type[0]));
                    il.Emit(OpCodes.Stsfld, statStringBuilder);

                    il.Emit(OpCodes.Ldsfld, statStringBuilder);
                    il.Emit(OpCodes.Ldstr, "In 64 bit exe");
                    il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));

                    //targ32bitDll = args[0];
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldc_I4, 0);
                    il.Emit(OpCodes.Ldelem_Ref);
                    il.Emit(OpCodes.Stsfld, statTarg32bitDll);

                    //var asmprog32 = Assembly.LoadFrom(args[0]);
                    il.Emit(OpCodes.Ldsfld, statTarg32bitDll);
                    il.Emit(OpCodes.Call, typeof(Assembly).GetMethod("LoadFrom", new Type[] { typeof(string) }));
                    il.Emit(OpCodes.Stloc_2);


                    //AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                    il.Emit(OpCodes.Call, typeof(AppDomain).GetProperty("CurrentDomain").GetMethod);
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Ldftn, AsmResolveMethodBuilder);
                    il.Emit(OpCodes.Newobj, typeof(ResolveEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
                    il.Emit(OpCodes.Callvirt, typeof(AppDomain).GetEvent("AssemblyResolve").GetAddMethod());

                    il.Emit(OpCodes.Ldsfld, statStringBuilder);
                    il.Emit(OpCodes.Ldstr, "Asm ResolveEvents");
                    il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));

                    //foreach (var type in asmprog32.GetExportedTypes())
                    il.Emit(OpCodes.Ldloc_2);
                    il.Emit(OpCodes.Callvirt, typeof(Assembly).GetMethod("GetExportedTypes"));
                    il.Emit(OpCodes.Stloc_3);

                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Stloc, 4);
                    var labIncLoop = il.DefineLabel();
                    il.Emit(OpCodes.Br_S, labIncLoop);
                    {
                        var labStartLoop = il.DefineLabel();
                        il.MarkLabel(labStartLoop);

                        il.Emit(OpCodes.Ldsfld, statStringBuilder);
                        il.Emit(OpCodes.Ldstr, "In loop");
                        il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                        il.Emit(OpCodes.Pop);


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
                        il.Emit(OpCodes.Blt_S, labStartLoop);
                    }



                }
                il.BeginCatchBlock(typeof(Exception)); // exception is on eval stack
                {
                    il.Emit(OpCodes.Call, typeof(Exception).GetMethod("ToString", new Type[0]));
                    il.Emit(OpCodes.Stloc_0);

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
                il.EndExceptionBlock();

                il.Emit(OpCodes.Ldsfld, statStringBuilder);
                il.Emit(OpCodes.Call, typeof(StringBuilder).GetMethod("ToString", new Type[0]));
                il.Emit(OpCodes.Stloc_0);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4, 4);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, typeof(File).GetMethod("WriteAllText", new Type[] { typeof(string), typeof(string) }));
                il.Emit(OpCodes.Ret);
            }
            typeBuilder.CreateType();
            assemblyBuilder.SetEntryPoint(mainMethodBuilder, PEFileKinds.WindowApplication);
            assemblyBuilder.Save($"{aName.Name}.exe", PortableExecutableKinds.PE32Plus, ImageFileMachine.AMD64);

        }


        public void TestCreate64BitExeUsingEmit()
        {
            var aName = new AssemblyName(Path.GetFileNameWithoutExtension(_targ64PEFile));
            // the Appdomain DefineDynamicAssembly has an overload for Dir
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(aName, AssemblyBuilderAccess.Save, dir: Path.GetDirectoryName(_targ64PEFile));
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(aName.Name + ".exe");
            var typeBuilder = moduleBuilder.DefineType(_TypeName, TypeAttributes.Public);
            var statTarg32bitDll = typeBuilder.DefineField("targ32bitDll", typeof(string), FieldAttributes.Static);
            var statStringBuilder = typeBuilder.DefineField("_StringBuilder", typeof(StringBuilder), FieldAttributes.Static);
            var AsmResolveMethodBuilder = typeBuilder.DefineMethod(
                "CurrentDomain_AssemblyResolve",
                MethodAttributes.Static,
                returnType: typeof(Assembly),
                parameterTypes: new Type[] { typeof(object), typeof(ResolveEventArgs) }
                );
            {
                var il = AsmResolveMethodBuilder.GetILGenerator();


                il.Emit(OpCodes.Ret);
            }
            var mainMethodBuilder = typeBuilder.DefineMethod(
                "Main",
                MethodAttributes.Public | MethodAttributes.Static,
                returnType: null,
                parameterTypes: new Type[] { typeof(string[]) });
            {
                var il = mainMethodBuilder.GetILGenerator();

                il.DeclareLocal(typeof(StringBuilder));
                il.DeclareLocal(typeof(string));
                il.DeclareLocal(typeof(DateTime));
                il.DeclareLocal(typeof(string));
                il.DeclareLocal(typeof(string));

                il.BeginExceptionBlock();
                {
                    il.Emit(OpCodes.Newobj, typeof(StringBuilder).GetConstructor(new Type[0]));
                    il.Emit(OpCodes.Stloc_0);

                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Stsfld, statStringBuilder);

                    il.Emit(OpCodes.Call, typeof(AppDomain).GetProperty("CurrentDomain").GetMethod);
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Ldftn, AsmResolveMethodBuilder);
                    il.Emit(OpCodes.Newobj, typeof(ResolveEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
                    il.Emit(OpCodes.Callvirt, typeof(AppDomain).GetEvent("AssemblyResolve").GetAddMethod());

                    for (int i = 0; i < 3; i++)
                    {
                        il.Emit(OpCodes.Ldloc_0);
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldc_I4, i);
                        il.Emit(OpCodes.Ldelem_Ref);
                        il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                        il.Emit(OpCodes.Pop);
                    }
                    //            var privAsmDir = Path.Combine(Path.GetDirectoryName(targ32bitDll), "PrivateAssemblies");

                    il.Emit(OpCodes.Ldstr, "string in static field");
                    il.Emit(OpCodes.Stsfld, statTarg32bitDll);


                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldc_I4, 0);
                    il.Emit(OpCodes.Ldelem_Ref);
                    il.Emit(OpCodes.Call, typeof(Path).GetMethod("GetDirectoryName"));
                    il.Emit(OpCodes.Ldstr, "PrivateAssemblies");
                    il.Emit(OpCodes.Call, typeof(Path).GetMethod("Combine", new Type[] { typeof(string), typeof(string) }));

                    il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));

                    //var requestName = args.Name.Substring(0, args.Name.IndexOf(",")); // Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldc_I4, 2);
                    il.Emit(OpCodes.Ldelem_Ref);
                    il.Emit(OpCodes.Stloc_1);

                    il.Emit(OpCodes.Ldloc_1);
                    il.Emit(OpCodes.Ldc_I4, 0);
                    il.Emit(OpCodes.Ldloc_1);

                    il.Emit(OpCodes.Ldstr, ",");
                    il.Emit(OpCodes.Callvirt, typeof(string).GetMethod("IndexOf", new Type[] { typeof(string) }));
                    il.Emit(OpCodes.Callvirt, typeof(string).GetMethod("Substring", new Type[] { typeof(Int32), typeof(Int32) }));
                    il.Emit(OpCodes.Stloc_3);
                    il.Emit(OpCodes.Ldloc_3);
                    il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));

                    il.Emit(OpCodes.Ldloc_3);
                    il.Emit(OpCodes.Ldstr, "Microsoft.VisualStudio.Telemetry");
                    il.Emit(OpCodes.Call, typeof(string).GetMethod("op_Equality", new Type[] { typeof(string), typeof(string) }));
                    var labIsNotVSTelem = il.DefineLabel();
                    il.Emit(OpCodes.Brfalse_S, labIsNotVSTelem);
                    {
                        il.Emit(OpCodes.Ldstr, "IsVsTelem");
                        il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                    }
                    il.MarkLabel(labIsNotVSTelem);

                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldsfld, statTarg32bitDll);
                    il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                }
                il.BeginCatchBlock(typeof(Exception)); // exception is on eval stack
                {
                    il.Emit(OpCodes.Call, typeof(Exception).GetMethod("ToString", new Type[0]));
                    il.Emit(OpCodes.Stloc_1);

                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Call, typeof(DateTime).GetProperty("Now").GetMethod);
                    il.Emit(OpCodes.Stloc_2);
                    il.Emit(OpCodes.Ldloca_S, 2);
                    il.Emit(OpCodes.Callvirt, typeof(DateTime).GetMethod("ToString", new Type[0]));
                    il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));

                    il.Emit(OpCodes.Ldstr, "Exception thrown");
                    il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));

                    il.Emit(OpCodes.Ldloc_1);
                    il.Emit(OpCodes.Call, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                    il.Emit(OpCodes.Pop);
                }
                il.EndExceptionBlock();

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, typeof(StringBuilder).GetMethod("ToString", new Type[0]));
                il.Emit(OpCodes.Stloc_1);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4, 0);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Ldloc_1);
                il.Emit(OpCodes.Call, typeof(File).GetMethod("WriteAllText", new Type[] { typeof(string), typeof(string) }));
                il.Emit(OpCodes.Ret);
            }
            typeBuilder.CreateType();
            assemblyBuilder.SetEntryPoint(mainMethodBuilder, PEFileKinds.WindowApplication);
            assemblyBuilder.Save($"{aName.Name}.exe", PortableExecutableKinds.PE32Plus, ImageFileMachine.AMD64);

        }

    }
}
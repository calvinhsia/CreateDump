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
            var statTarg32bitDll= typeBuilder.DefineField("targ32bitDll", typeof(string), FieldAttributes.Static);

            var methodBuilder = typeBuilder.DefineMethod("Main", MethodAttributes.Public | MethodAttributes.Static, null, new Type[] { typeof(string[]) });
            var il = methodBuilder.GetILGenerator();

            il.DeclareLocal(typeof(StringBuilder));
            il.DeclareLocal(typeof(string));
            il.DeclareLocal(typeof(DateTime));
            il.DeclareLocal(typeof(string));
            il.DeclareLocal(typeof(string));

            il.BeginExceptionBlock();
            {
                il.Emit(OpCodes.Newobj, typeof(StringBuilder).GetConstructor(new Type[0]));
                il.Emit(OpCodes.Stloc_0);

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

            typeBuilder.CreateType();
            assemblyBuilder.SetEntryPoint(methodBuilder, PEFileKinds.WindowApplication);
            assemblyBuilder.Save($"{aName.Name}.exe", PortableExecutableKinds.PE32Plus, ImageFileMachine.AMD64);

        }


        public void CreateAssembly()
        {
            AssemblyName aName = new AssemblyName("DynamicAssemblyExample");
            AssemblyBuilder ab =
                AppDomain.CurrentDomain.DefineDynamicAssembly(
                    aName,
                    AssemblyBuilderAccess.RunAndSave);

            // For a single-module assembly, the module name is usually
            // the assembly name plus an extension.
            ModuleBuilder mb =
                ab.DefineDynamicModule(aName.Name, aName.Name + ".dll");

            TypeBuilder tb = mb.DefineType(
                "MyDynamicType",
                 TypeAttributes.Public);

            // Add a private field of type int (Int32).
            FieldBuilder fbNumber = tb.DefineField(
                "m_number",
                typeof(int),
                FieldAttributes.Private);

            // Define a constructor that takes an integer argument and
            // stores it in the private field.
            Type[] parameterTypes = { typeof(int) };
            ConstructorBuilder ctor1 = tb.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                parameterTypes);

            ILGenerator ctor1IL = ctor1.GetILGenerator();
            // For a constructor, argument zero is a reference to the new
            // instance. Push it on the stack before calling the base
            // class constructor. Specify the default constructor of the
            // base class (System.Object) by passing an empty array of
            // types (Type.EmptyTypes) to GetConstructor.
            ctor1IL.Emit(OpCodes.Ldarg_0);
            ctor1IL.Emit(OpCodes.Call,
                typeof(object).GetConstructor(Type.EmptyTypes));
            // Push the instance on the stack before pushing the argument
            // that is to be assigned to the private field m_number.
            ctor1IL.Emit(OpCodes.Ldarg_0);
            ctor1IL.Emit(OpCodes.Ldarg_1);
            ctor1IL.Emit(OpCodes.Stfld, fbNumber);
            ctor1IL.Emit(OpCodes.Ret);

            // Define a default constructor that supplies a default value
            // for the private field. For parameter types, pass the empty
            // array of types or pass null.
            ConstructorBuilder ctor0 = tb.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                Type.EmptyTypes);

            ILGenerator ctor0IL = ctor0.GetILGenerator();
            // For a constructor, argument zero is a reference to the new
            // instance. Push it on the stack before pushing the default
            // value on the stack, then call constructor ctor1.
            ctor0IL.Emit(OpCodes.Ldarg_0);
            ctor0IL.Emit(OpCodes.Ldc_I4_S, 42);
            ctor0IL.Emit(OpCodes.Call, ctor1);
            ctor0IL.Emit(OpCodes.Ret);

            // Define a property named Number that gets and sets the private
            // field.
            //
            // The last argument of DefineProperty is null, because the
            // property has no parameters. (If you don't specify null, you must
            // specify an array of Type objects. For a parameterless property,
            // use the built-in array with no elements: Type.EmptyTypes)
            PropertyBuilder pbNumber = tb.DefineProperty(
                "Number",
                PropertyAttributes.HasDefault,
                typeof(int),
                null);

            // The property "set" and property "get" methods require a special
            // set of attributes.
            MethodAttributes getSetAttr = MethodAttributes.Public |
                MethodAttributes.SpecialName | MethodAttributes.HideBySig;

            // Define the "get" accessor method for Number. The method returns
            // an integer and has no arguments. (Note that null could be
            // used instead of Types.EmptyTypes)
            MethodBuilder mbNumberGetAccessor = tb.DefineMethod(
                "get_Number",
                getSetAttr,
                typeof(int),
                Type.EmptyTypes);

            ILGenerator numberGetIL = mbNumberGetAccessor.GetILGenerator();
            // For an instance property, argument zero is the instance. Load the
            // instance, then load the private field and return, leaving the
            // field value on the stack.
            numberGetIL.Emit(OpCodes.Ldarg_0);
            numberGetIL.Emit(OpCodes.Ldfld, fbNumber);
            numberGetIL.Emit(OpCodes.Ret);

            // Define the "set" accessor method for Number, which has no return
            // type and takes one argument of type int (Int32).
            MethodBuilder mbNumberSetAccessor = tb.DefineMethod(
                "set_Number",
                getSetAttr,
                null,
                new Type[] { typeof(int) });

            ILGenerator numberSetIL = mbNumberSetAccessor.GetILGenerator();
            // Load the instance and then the numeric argument, then store the
            // argument in the field.
            numberSetIL.Emit(OpCodes.Ldarg_0);
            numberSetIL.Emit(OpCodes.Ldarg_1);
            numberSetIL.Emit(OpCodes.Stfld, fbNumber);
            numberSetIL.Emit(OpCodes.Ret);

            // Last, map the "get" and "set" accessor methods to the
            // PropertyBuilder. The property is now complete.
            pbNumber.SetGetMethod(mbNumberGetAccessor);
            pbNumber.SetSetMethod(mbNumberSetAccessor);

            // Define a method that accepts an integer argument and returns
            // the product of that integer and the private field m_number. This
            // time, the array of parameter types is created on the fly.
            MethodBuilder meth = tb.DefineMethod(
                "MyMethod",
                MethodAttributes.Public,
                typeof(int),
                new Type[] { typeof(int) });

            ILGenerator methIL = meth.GetILGenerator();
            // To retrieve the private instance field, load the instance it
            // belongs to (argument zero). After loading the field, load the
            // argument one and then multiply. Return from the method with
            // the return value (the product of the two numbers) on the
            // execution stack.
            methIL.Emit(OpCodes.Ldarg_0);
            methIL.Emit(OpCodes.Ldfld, fbNumber);
            methIL.Emit(OpCodes.Ldarg_1);
            methIL.Emit(OpCodes.Mul);
            methIL.Emit(OpCodes.Ret);

            // Finish the type.
            Type t = tb.CreateType();

            // The following line saves the single-module assembly. This
            // requires AssemblyBuilderAccess to include Save. You can now
            // type "ildasm MyDynamicAsm.dll" at the command prompt, and
            // examine the assembly. You can also write a program that has
            // a reference to the assembly, and use the MyDynamicType type.
            //
            ab.Save(aName.Name + ".dll"); // will overwrite if exists.
                                          //            ab.Save(aName.Name + ".exe", PortableExecutableKinds.PE32Plus, ImageFileMachine.AMD64); // will overwrite if exists.

            // Because AssemblyBuilderAccess includes Run, the code can be
            // executed immediately. Start by getting reflection objects for
            // the method and the property.
            MethodInfo mi = t.GetMethod("MyMethod");
            PropertyInfo pi = t.GetProperty("Number");

            // Create an instance of MyDynamicType using the default
            // constructor.
            object o1 = Activator.CreateInstance(t);

            // Display the value of the property, then change it to 127 and
            // display it again. Use null to indicate that the property
            // has no index.
            Console.WriteLine("o1.Number: {0}", pi.GetValue(o1, null));
            pi.SetValue(o1, 127, null);
            Console.WriteLine("o1.Number: {0}", pi.GetValue(o1, null));

            // Call MyMethod, passing 22, and display the return value, 22
            // times 127. Arguments must be passed as an array, even when
            // there is only one.
            object[] arguments = { 22 };
            Console.WriteLine("o1.MyMethod(22): {0}",
                mi.Invoke(o1, arguments));

            // Create an instance of MyDynamicType using the constructor
            // that specifies m_Number. The constructor is identified by
            // matching the types in the argument array. In this case,
            // the argument array is created on the fly. Display the
            // property value.
            object o2 = Activator.CreateInstance(t,
                new object[] { 5280 });
            Console.WriteLine("o2.Number: {0}", pi.GetValue(o2, null));

        }
    }
}
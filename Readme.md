Taking a process dump of a 64 bit process from a 32 bit process doesn't work. Even from 32 bit task manager.
This code emits an Asm that can be made into a 64 bit executable
The goal is to call a static method in PerfWatson in an internal static class MemoryDumpHelper with the signature:
          public static void CollectDumpSimulator(int procid, string pathOutput, bool FullHeap)
The generated asm can be saved as an exe on disk, then started from 32 bit code. 
 A little wrinkle: in order to enumerate the types in the DLL, the Appdomain AsemblyResolver needs to find the dependencies
The 64 bit process will then load the 32 bit PW IL (using the assembly resolver, then invoke the method via reflection)
the parameters are pased to the 64 bit exe on the commandline.
This code logs output to the output file (which is the dump file when called with logging false)
The code generates a static Main (string[] args) method.

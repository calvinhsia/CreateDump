#include "pch.h"

#include <minidumpapiset.h>
#include <processsnapshot.h>



BOOL CALLBACK MyMiniDumpWriteDumpCallback(
	__in     PVOID CallbackParam,
	__in     const PMINIDUMP_CALLBACK_INPUT CallbackInput,
	__inout  PMINIDUMP_CALLBACK_OUTPUT CallbackOutput
)
{
	switch (CallbackInput->CallbackType)
	{
	case 16: // IsProcessSnapshotCallback
		CallbackOutput->Status = S_FALSE;
		break;
	}
	return TRUE;
}

void createdump(int fUseSnapshot)
{
	auto pidDevenv64 = 60016;
	auto hDevenv = OpenProcess(PROCESS_ALL_ACCESS, false, pidDevenv64);
	//	auto hDevenv = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_DUP_HANDLE, false, pidDevenv64);
	auto dumpFilePath = L"c:\\t1.dmp";
	DeleteFile(dumpFilePath);

	auto hFile = CreateFile( // will overwrite file if exists.
		dumpFilePath,
		GENERIC_WRITE,
		0,//EFileShare.None,
		NULL,//lpSecurityAttributes : IntPtr.Zero,
		CREATE_ALWAYS,
		FILE_ATTRIBUTE_NORMAL,
		nullptr//hTemplateFile : IntPtr.Zero
	);
	MINIDUMP_EXCEPTION_INFORMATION excepinfo = { 0 };
	auto dumpFlags = MiniDumpNormal
		| MiniDumpWithHandleData
		| MiniDumpWithThreadInfo

		| MiniDumpWithFullMemory
		| MiniDumpWithFullMemoryInfo
		;

	if (fUseSnapshot == 1)
	{ // CxlWerHelperRoutines  https://microsoft.visualstudio.com/DefaultCollection/OS/_git/0d54b6ef-7283-444f-847a-343728d58a4d?path=%2fservercommon%2fbase%2fcluster%2fcxlrtl%2fCxlWerHelperRoutines.cpp&version=GBofficial/main
		auto CaptureFlags = PSS_CAPTURE_VA_CLONE
			| PSS_CAPTURE_HANDLES
			| PSS_CAPTURE_HANDLE_NAME_INFORMATION
			| PSS_CAPTURE_HANDLE_BASIC_INFORMATION
			| PSS_CAPTURE_HANDLE_TYPE_SPECIFIC_INFORMATION
			| PSS_CAPTURE_HANDLE_TRACE
			| PSS_CAPTURE_THREADS
			| PSS_CAPTURE_THREAD_CONTEXT
			| PSS_CAPTURE_THREAD_CONTEXT_EXTENDED
			//			| PSS_CAPTURE_VA_SPACE
						//| PSS_CAPTURE_VA_SPACE_SECTION_INFORMATION
			| PSS_CAPTURE_IPT_TRACE
			| PSS_CREATE_BREAKAWAY
			| PSS_CREATE_BREAKAWAY_OPTIONAL
			| PSS_CREATE_USE_VM_ALLOCATIONS
			| PSS_CREATE_RELEASE_SECTION;

		MINIDUMP_CALLBACK_INFORMATION CallbackInfo = { 0 };
		CallbackInfo.CallbackRoutine = MyMiniDumpWriteDumpCallback;
		CallbackInfo.CallbackParam = NULL;

		HPSS hSnapshot = NULL;
		if (PssCaptureSnapshot(hDevenv,
			CaptureFlags,
			CONTEXT_ALL, // DWORD ThreadContextFlags
			&hSnapshot
		) == S_OK)
		{

			if (!MiniDumpWriteDump(
				hSnapshot,
				pidDevenv64,
				hFile,
				(MINIDUMP_TYPE)dumpFlags,
				&excepinfo,
				NULL, // UserStreamParam
				&CallbackInfo// callback

			))
			{
				auto hr = GetLastError();

			}
			auto x = 2;
			auto res2 = CloseHandle(hFile);
			auto res = PssFreeSnapshot(GetCurrentProcess(), hSnapshot);

		}
		else
		{
			auto hr = GetLastError();
		}
	}
	else
	{
		if (MiniDumpWriteDump(
			hDevenv,
			pidDevenv64,
			hFile,
			(MINIDUMP_TYPE)dumpFlags,
			&excepinfo,
			NULL, // UserStreamParam
			nullptr // callback

		))
		{


		}
		auto res2 = CloseHandle(hFile);
	}



	auto res = CloseHandle(hDevenv);
}



extern "C" int __declspec(dllexport) __stdcall CreateDump(int UseSnapshot)
{
	createdump(UseSnapshot);
	return 0;
}

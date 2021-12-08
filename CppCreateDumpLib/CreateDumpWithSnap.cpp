#include "pch.h"

#include <minidumpapiset.h>
#include <processsnapshot.h>
#include <atlbase.h>
#include <atlcom.h>
#include <initguid.h>

#import "..\Test64\bin\debug\Test64.tlb" no_namespace




BOOL CALLBACK MyMiniDumpWriteDumpCallback(
	__in     PVOID CallbackParam,
	__in     const PMINIDUMP_CALLBACK_INPUT CallbackInput,
	__inout  PMINIDUMP_CALLBACK_OUTPUT CallbackOutput
)
{
	switch (CallbackInput->CallbackType)
	{
	case IsProcessSnapshotCallback:
		CallbackOutput->Status = S_FALSE;
		break;
	}
	return TRUE;
}

int createdump(int pidToDump, int fUseSnapshot, LPCWSTR dumpFilePath, __int64 hSnapshotFromCaller)
{
	HRESULT hr = S_OK;
	auto hDevenv = OpenProcess(PROCESS_ALL_ACCESS, false, pidToDump);
	//	auto hDevenv = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_DUP_HANDLE, false, pidDevenv64);
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
	WCHAR dumpComment[1024] = L"This is a comment";
	MINIDUMP_USER_STREAM UserStreams[1];
	UserStreams[0].Type = CommentStreamW;
	UserStreams[0].BufferSize = (ULONG)wcslen(dumpComment) * sizeof(WCHAR);
	UserStreams[0].Buffer = dumpComment;

	MINIDUMP_USER_STREAM_INFORMATION userStreamInfo;
	userStreamInfo.UserStreamCount = 1;
	userStreamInfo.UserStreamArray = UserStreams;

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
			| PSS_CAPTURE_VA_SPACE
			| PSS_CAPTURE_VA_SPACE_SECTION_INFORMATION
			//| PSS_CAPTURE_IPT_TRACE
			//| PSS_CREATE_BREAKAWAY
			//| PSS_CREATE_BREAKAWAY_OPTIONAL
			//| PSS_CREATE_USE_VM_ALLOCATIONS
			//| PSS_CREATE_RELEASE_SECTION
			;

		MINIDUMP_CALLBACK_INFORMATION CallbackInfo = { 0 };
		CallbackInfo.CallbackRoutine = MyMiniDumpWriteDumpCallback;
		CallbackInfo.CallbackParam = NULL;

		HPSS hSnapshot = NULL;
		if (hSnapshotFromCaller != 0)
		{
			hSnapshot = reinterpret_cast<HPSS>(hSnapshotFromCaller);
		}
		auto tcontext = CONTEXT_ALL;
		if (hSnapshotFromCaller != 0 || PssCaptureSnapshot(hDevenv,
				CaptureFlags,
				tcontext, // DWORD ThreadContextFlags
				&hSnapshot
			) == S_OK)
		{
			if (!MiniDumpWriteDump(
				hSnapshot,
				pidToDump,
				hFile,
				(MINIDUMP_TYPE)dumpFlags,
				&excepinfo,
				&userStreamInfo, // UserStreamParam
				&CallbackInfo// callback

			))
			{
				auto hr = GetLastError();

			}
			auto res2 = CloseHandle(hFile);
			auto res = PssFreeSnapshot(GetCurrentProcess(), hSnapshot);

		}
		else
		{
			hr = GetLastError();
		}
	}
	else
	{
		if (!MiniDumpWriteDump(
			hDevenv,
			pidToDump,
			hFile,
			(MINIDUMP_TYPE)dumpFlags,
			&excepinfo,
			NULL, // UserStreamParam
			nullptr // callback

		))
		{
			hr = GetLastError();

		}
		auto res2 = CloseHandle(hFile);
	}
	auto res = CloseHandle(hDevenv);
	return hr;
}



extern "C" int __declspec(dllexport) __stdcall CreateDump(int pidToDump, int UseSnapshot, LPCWSTR dumpFilePath)
{
	HRESULT hr = createdump(pidToDump, UseSnapshot, dumpFilePath, 0);
	return hr;
}

// {E3E00445-C0EA-4AB2-BF1C-309358F7EC3A}
DEFINE_GUID(CLSID_CreateDump ,
	0xe3e00445, 0xc0ea, 0x4ab2, 0xbf, 0x1c, 0x30, 0x93, 0x58, 0xf7, 0xec, 0x3a);


class MyCreateDump :
	public ICreateDump,
	public CComObjectRootEx<CComSingleThreadModel>,
	public CComCoClass<MyCreateDump, &CLSID_CreateDump>

{
public:
	BEGIN_COM_MAP(MyCreateDump)
		COM_INTERFACE_ENTRY_IID(CLSID_CreateDump, MyCreateDump)
		COM_INTERFACE_ENTRY(ICreateDump)
	END_COM_MAP()
	DECLARE_NOT_AGGREGATABLE(MyCreateDump)
	DECLARE_NO_REGISTRY()

	HRESULT __stdcall raw_CreateDump(
		/*[in]*/ long PidToDump,
		/*[in]*/ long UseSnapshot,
		/*[in]*/ BSTR pathDumpFileName,
		/*[out,retval]*/ long* pRetVal)
	{
		HRESULT hr = createdump(PidToDump, UseSnapshot, pathDumpFileName, 0);
		return hr;
	}
	virtual HRESULT __stdcall raw_CreateDumpFromPSSSnapshot(
		/*[in]*/ long PidToDump,
		/*[in]*/ __int64 hSnapshot,
		/*[in]*/ BSTR pathDumpFileName,
		/*[out,retval]*/ long* pRetVal)
	{
		HRESULT hr = createdump(PidToDump, 1, pathDumpFileName, hSnapshot);
		return hr;
	}

};

OBJECT_ENTRY_AUTO(CLSID_CreateDump, MyCreateDump)

// define a class that represents this module
class CCreateDumpModule : public ATL::CAtlDllModuleT< CCreateDumpModule >
{
#if _DEBUG
public:
	CCreateDumpModule()
	{
		int x = 0; // set a bpt here
	}
	~CCreateDumpModule()
	{
		int x = 0; // set a bpt here
	}
#endif _DEBUG
};


// instantiate a static instance of this class on module load
CCreateDumpModule _AtlModule;
// this gets called by CLR due to env var settings
_Check_return_
STDAPI DllGetClassObject(__in REFCLSID rclsid, __in REFIID riid, __deref_out LPVOID FAR* ppv)
{
	HRESULT hr = E_FAIL;
	hr = AtlComModuleGetClassObject(&_AtlComModule, rclsid, riid, ppv);
	//  hr= CComModule::GetClassObject();
	return hr;
}
//tell the linker to export the function
#pragma comment(linker, "/EXPORT:DllGetClassObject,PRIVATE")
//#pragma comment(linker, "/EXPORT:DllGetClassObject=_DllGetClassObject@12,PRIVATE")

__control_entrypoint(DllExport)
STDAPI DllCanUnloadNow()
{
	return S_OK;
}
//tell the linker to export the function
#pragma comment(linker, "/EXPORT:DllCanUnloadNow,PRIVATE")
//#pragma comment(linker, "/EXPORT:DllCanUnloadNow=_DllCanUnloadNow@0,PRIVATE")



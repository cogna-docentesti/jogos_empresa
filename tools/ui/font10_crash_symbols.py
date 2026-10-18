"""Resolve the recorded native Player fault using the installed Unity symbols."""
import ctypes as c
from ctypes import wintypes as w
import json
import struct
import uuid
import sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
SDK=Path('C:/Program Files (x86)/Windows Kits/10/Debuggers/x64')
release='--release' in sys.argv
variant='win64_player_nondevelopment_mono' if release else 'win64_player_development_mono'
SYMBOLS=Path('C:/Program Files/Unity Editors/6000.3.20f1/Editor/Data/PlaybackEngines/WindowsStandaloneSupport/Variations')/variant
pdb_path=next(SYMBOLS.glob('UnityPlayer*.pdb'))
image_bytes=(SYMBOLS/'UnityPlayer.dll').read_bytes();rsds=image_bytes.index(b'RSDS')
image_guid=image_bytes[rsds+4:rsds+20];image_age=struct.unpack_from('<I',image_bytes,rsds+20)[0]
pdb=pdb_path.read_bytes()
page,_,_,directory_length,_,directory_map=struct.unpack_from('<6I',pdb,32)
directory_pages=struct.unpack_from('<'+'I'*((directory_length+page-1)//page),pdb,directory_map*page)
directory=b''.join(pdb[i*page:(i+1)*page] for i in directory_pages)[:directory_length]
stream_count=struct.unpack_from('<I',directory)[0];stream_sizes=struct.unpack_from('<'+'I'*stream_count,directory,4)
cursor=4+4*stream_count
for i,size in enumerate(stream_sizes):
    pages=0 if size==0xffffffff else (size+page-1)//page
    ids=struct.unpack_from('<'+'I'*pages,directory,cursor);cursor+=4*pages
    if i==1:
        info=b''.join(pdb[x*page:(x+1)*page] for x in ids)[:size]
        assert info[12:28]==image_guid and struct.unpack_from('<I',info,8)[0]==image_age
        break
dbg=c.WinDLL(str(SDK/'dbghelp.dll'),use_last_error=True)
kernel=c.WinDLL('kernel32',use_last_error=True)
kernel.GetCurrentProcess.restype=w.HANDLE
handle=kernel.GetCurrentProcess()
dbg.SymInitializeW.argtypes=[w.HANDLE,w.LPCWSTR,w.BOOL]
dbg.SymInitializeW.restype=w.BOOL
dbg.SymSetOptions.argtypes=[w.DWORD]
dbg.SymSetOptions.restype=w.DWORD
dbg.SymLoadModuleExW.argtypes=[w.HANDLE,w.HANDLE,w.LPCWSTR,w.LPCWSTR,c.c_ulonglong,w.DWORD,c.c_void_p,w.DWORD]
dbg.SymLoadModuleExW.restype=c.c_ulonglong
class Symbol(c.Structure):
    _fields_=[('SizeOfStruct',w.ULONG),('TypeIndex',w.ULONG),('Reserved',c.c_ulonglong*2),('Index',w.ULONG),('Size',w.ULONG),('ModBase',c.c_ulonglong),('Flags',w.ULONG),('Value',c.c_ulonglong),('Address',c.c_ulonglong),('Register',w.ULONG),('Scope',w.ULONG),('Tag',w.ULONG),('NameLen',w.ULONG),('MaxNameLen',w.ULONG),('Name',w.WCHAR*1)]
dbg.SymFromAddrW.argtypes=[w.HANDLE,c.c_ulonglong,c.POINTER(c.c_ulonglong),c.POINTER(Symbol)]
dbg.SymFromAddrW.restype=w.BOOL
dbg.SymSetOptions(0x2|0x4|0x10|0x200|0x80000)
assert dbg.SymInitializeW(handle,str(SYMBOLS),False),c.WinError(c.get_last_error())
base=dbg.SymLoadModuleExW(handle,None,str(pdb_path),'UnityPlayer',0x10000000,0x6000000,None,0)
assert base,c.WinError(c.get_last_error())
buffer=c.create_string_buffer(c.sizeof(Symbol)+4096*2)
symbol=c.cast(buffer,c.POINTER(Symbol));symbol.contents.SizeOfStruct=c.sizeof(Symbol);symbol.contents.MaxNameLen=4096
displacement=c.c_ulonglong()
pid=json.loads((ROOT/'output/ui-update/fonts/FONT-10/windows-player/launch.json').read_text(encoding='utf-8'))['pid'] if release else 40688
dump=Path(f'C:/Users/Pichau/AppData/Local/CrashDumps/Cogna.exe.{pid}.dmp').read_bytes()
count,table=struct.unpack_from('<II',dump,8)
streams={kind:(size,rva) for kind,size,rva in (struct.unpack_from('<III',dump,table+12*i) for i in range(count))}
exception=streams[6][1];thread_id=struct.unpack_from('<I',dump,exception)[0]
context_rva=struct.unpack_from('<I',dump,exception+164)[0]
rsp=struct.unpack_from('<Q',dump,context_rva+152)[0]
modules_rva=streams[4][1];module_count=struct.unpack_from('<I',dump,modules_rva)[0]
unity_base=unity_size=None
for i in range(module_count):
    offset=modules_rva+4+108*i
    image_base,image_size=struct.unpack_from('<QI',dump,offset)
    name_rva=struct.unpack_from('<I',dump,offset+20)[0];length=struct.unpack_from('<I',dump,name_rva)[0]
    module_name=dump[name_rva+4:name_rva+4+length].decode('utf-16le')
    if module_name.endswith('UnityPlayer.dll'):
        unity_base,unity_size=image_base,image_size
        cv_size,cv_rva=struct.unpack_from('<II',dump,offset+76)
        assert dump[cv_rva:cv_rva+4]==b'RSDS' and dump[cv_rva+4:cv_rva+20]==image_guid
fault_offset=struct.unpack_from('<Q',dump,exception+24)[0]-unity_base
assert dbg.SymFromAddrW(handle,base+fault_offset,c.byref(displacement),symbol),c.WinError(c.get_last_error())
name=c.wstring_at(c.addressof(buffer)+Symbol.Name.offset,symbol.contents.NameLen)
threads_rva=streams[3][1];thread_count=struct.unpack_from('<I',dump,threads_rva)[0]
stack=[]
for i in range(thread_count):
    offset=threads_rva+4+48*i
    if struct.unpack_from('<I',dump,offset)[0]!=thread_id:continue
    stack_start,stack_size,stack_rva=struct.unpack_from('<QII',dump,offset+24)
    first=stack_rva+rsp-stack_start
    for offset in range(first,min(first+1536,stack_rva+stack_size),8):
        address=struct.unpack_from('<Q',dump,offset)[0]
        if not unity_base<=address<unity_base+unity_size:continue
        if dbg.SymFromAddrW(handle,base+address-unity_base,c.byref(displacement),symbol):
            stack.append({'stack_offset':offset-first,'rva':hex(address-unity_base),'symbol':c.wstring_at(c.addressof(buffer)+Symbol.Name.offset,symbol.contents.NameLen),'displacement':displacement.value})
result={'module':'UnityPlayer.dll','fault_offset':hex(fault_offset),'symbol':name,'release':release,'pid':pid,'installed_symbols':str(SYMBOLS),'symbol_server_downloaded':False,'matching_pdb_guid':str(uuid.UUID(bytes_le=image_guid)),'age':image_age,'stack_pointer_scan_not_unwound':stack,'limit':'Symbol names can represent linker-folded functions. This is a stack-pointer scan, not a debugger-unwound call stack; it supports native static-cleanup localization rather than a proven source-level engine cause.'}
path=ROOT/('output/ui-update/fonts/FONT-10/native-fault-release-symbol.json' if release else 'output/ui-update/fonts/FONT-10/native-fault-symbol.json')
path.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result))

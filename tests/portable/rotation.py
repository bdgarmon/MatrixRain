"""Real worker-process rotation/lease/stop test; desktop helper is a pipe fixture.
PORTABLE_TEST alone accelerates phases to one second. Windows UI is not exercised.
"""
import json, os, pathlib, socket, subprocess, tempfile, time, uuid
host=pathlib.Path(__file__).resolve().parent/'bin/Debug/net10.0/TestHost.dll'
def read(d):
    return json.loads((d/'workloads/run.json').read_text())
with tempfile.TemporaryDirectory(prefix='Matrix rotation ') as folder:
    d=pathlib.Path(folder); rid=uuid.uuid4().hex
    p=subprocess.Popen(['dotnet',str(host),'--data',str(d),'--run-id',rid,'--mode','rotation','--actor-sid','fixture','--actor-session','1'],stdout=subprocess.PIPE,stderr=subprocess.PIPE)
    sockets={}; phases={}; helpers=[]; deadline=time.monotonic()+65
    try:
        while time.monotonic()<deadline:
            try: r=read(d)
            except (FileNotFoundError,json.JSONDecodeError): time.sleep(.05); continue
            assert r['State']=='RUNNING',r
            phases[r['PhaseIndex']]=r['Phase']
            assert r['Mode']=='rotation' and r['PlannedSeconds']==0 and r['PhaseCount']==0
            assert r['Phase']!='office'
            if r['Phase']=='windows':
                helper=r['HelperId']
                if helper not in sockets:
                    s=socket.socket(socket.AF_UNIX,socket.SOCK_STREAM)
                    s.connect(os.path.join(tempfile.gettempdir(),'CoreFxPipe_AfterSix.MatrixOffice.'+helper))
                    sockets[helper]=s; helpers.append(helper)
                    s.sendall((json.dumps({'State':'COMPLETED','UserSid':'fixture','Pid':os.getpid(),'Actions':3,'Saved':2,'BytesSaved':128})+'\n').encode())
            if len(helpers)>=2 and r['Phase']!='windows': break
            time.sleep(.05)
        assert len(helpers)>=2,'Two repeated Windows helper phases were not observed'
        assert len(set(helpers))==len(helpers),'Helpers must have independent authorization and pipes'
        assert all(mode=='visualization' for i,mode in phases.items() if i%2==0),'Rest phase missing'
        q=subprocess.run(['dotnet',str(host),'--data',str(d),'--mode','cpu','--seconds','1'],capture_output=True)
        assert q.returncode==1 and read(d)['RunId']==rid,'Rotation lost the exclusive worker lease'
        stop=subprocess.run(['dotnet',str(host),'--data',str(d),'--stop',rid],capture_output=True,timeout=10)
        p.communicate(timeout=10)
        final=read(d)
        assert stop.returncode==0 and p.returncode==0 and final['State']=='IDLE',final
        assert (d/'workloads/last-phase.json').exists()
        print('PASS: repeated rotation transitions, screensaver rest, exclusion, unique helper pipes, exclusive lease, operator Stop, retained phase result')
    finally:
        if p.poll() is None: p.kill(); p.communicate()
        for s in sockets.values(): s.close()

with tempfile.TemporaryDirectory(prefix='Matrix app stop ') as folder:
    d=pathlib.Path(folder); rid=uuid.uuid4().hex
    p=subprocess.Popen(['dotnet',str(host),'--data',str(d),'--run-id',rid,'--mode','windows','--seconds','60','--actor-sid','fixture','--actor-session','1'],stdout=subprocess.PIPE,stderr=subprocess.PIPE)
    s=None
    try:
        deadline=time.monotonic()+5
        while True:
            try:
                r=read(d)
                if r['Phase']=='windows': break
            except (FileNotFoundError,json.JSONDecodeError): pass
            assert time.monotonic()<deadline
            time.sleep(.05)
        s=socket.socket(socket.AF_UNIX,socket.SOCK_STREAM)
        s.connect(os.path.join(tempfile.gettempdir(),'CoreFxPipe_AfterSix.MatrixOffice.'+r['HelperId']))
        status={'State':'ACTIVE','UserSid':'fixture','Pid':os.getpid(),'Actions':3,'Saved':2,'BytesSaved':128}
        s.sendall((json.dumps(status)+'\n').encode())
        stopper=subprocess.Popen(['dotnet',str(host),'--data',str(d),'--stop',rid],stdout=subprocess.PIPE,stderr=subprocess.PIPE)
        deadline=time.monotonic()+5
        while not (d/f'workloads/stop-{rid}').exists():
            assert time.monotonic()<deadline
            time.sleep(.05)
        assert stopper.poll() is None,'Stop returned before app cleanup acknowledgment'
        status['State']='COMPLETED'
        s.sendall((json.dumps(status)+'\n').encode())
        stopper.communicate(timeout=8); p.communicate(timeout=8)
        final=read(d)
        assert p.returncode==0 and stopper.returncode==0 and final['State']=='IDLE' and final['Result']['Office']['Saved']==2
        print('PASS: Stop during Windows Workday waits for helper cleanup and retains saved-file counters')
    finally:
        if p.poll() is None: p.kill(); p.communicate()
        if s: s.close()

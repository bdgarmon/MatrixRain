import subprocess,pathlib,json,time,uuid,tempfile
host=pathlib.Path(__file__).resolve().parent/'bin/Debug/net10.0/TestHost.dll'
count=0
def check(value,label):
 global count
 assert value,label
 count+=1;print('PASS:',label,flush=True)
def run(data,mode='cpu',seconds=2,baseline=1,extra=()):
 rid=uuid.uuid4().hex
 p=subprocess.Popen(['dotnet',str(host),'--data',str(data),'--run-id',rid,'--mode',mode,'--seconds',str(seconds),'--baseline-seconds',str(baseline),'--threads','1','--intensity','10',*extra],stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
 return p,rid
def read(data):return json.loads((data/'workloads/run.json').read_text())
def phase(data,rid,name):
 for _ in range(150):
  try:
   r=read(data)
   if r['RunId']==rid and r['Phase']==name:return r
  except (FileNotFoundError,json.JSONDecodeError):pass
  time.sleep(.02)
 raise AssertionError('Phase never appeared: '+name)
with tempfile.TemporaryDirectory(prefix='Matrix sequence ') as folder:
 d=pathlib.Path(folder)
 p,rid=run(d)
 first=phase(d,rid,'baseline')
 check(first['Mode']=='cpu' and first['PhaseCount']==2 and first['PlannedSeconds']==3,'Go creates a two-phase experiment with target mode and total duration')
 check(json.loads((d/'state.json').read_text())['RainEnabled'] is False,'Baseline requests rain off')
 second,rid2=run(d)
 second.communicate(timeout=5)
 check(second.returncode==1 and read(d)['RunId']==rid,'Exclusive lock spans baseline and rejects another experiment')
 active=phase(d,rid,'cpu')
 check(active['PhaseIndex']==2 and active['Pid']==first['Pid'],'Same elevated worker transitions to workload')
 check(json.loads((d/'state.json').read_text())['RainEnabled'] is True,'Workload restores rain')
 p.communicate(timeout=8); final=read(d)
 check(p.returncode==0 and final['State']=='COMPLETED' and len(final['Phases'])==2,'Both phases complete under one run ID')
 check(final['Phases'][0]['Phase']=='baseline' and final['Phases'][1]['Result']['Reason']=='duration reached','Results preserve baseline and workload timings and stop reason')
 check(json.loads((d/'workloads/last-result.json').read_text())['RunId']==rid,'Last result survives the next experiment')
 p,rid=run(d,baseline=5)
 phase(d,rid,'baseline')
 stopper=subprocess.run(['dotnet',str(host),'--data',str(d),'--stop',rid,'--run-id',uuid.uuid4().hex],capture_output=True,timeout=8)
 p.communicate(timeout=8); final=read(d)
 check(stopper.returncode==0 and final['State']=='IDLE' and final['Phase']=='baseline','Stop during baseline cancels the whole experiment')
 check(len(final['Phases'])==1 and final['Detail']=='stopped by operator during baseline','Cancelled baseline never starts the pressure workload')
 p,rid=run(d,seconds=6)
 phase(d,rid,'cpu')
 (d/f'workloads/stop-{rid}').write_text('stop')
 p.communicate(timeout=8)
 check(read(d)['State']=='IDLE' and len(read(d)['Phases'])==2,'Stop during workload retains the completed baseline')
 p,rid=run(d,mode='disk',seconds=3,extra=('--disk-mib','4','--write-limit-mib','4','--disk-rate-mib','256'))
 p.communicate(timeout=8); final=read(d)
 check(final['State']=='COMPLETED' and 'budget' in final['Detail'],'Write-budget completion reports its real reason')
 p,rid=run(d,mode='baseline',seconds=1,baseline=60)
 p.communicate(timeout=5)
 check(read(d)['PhaseCount']==1 and len(read(d)['Phases'])==1,'Explicit standalone baseline is not duplicated')
 p,rid=run(d,seconds=3600,baseline=60)
 p.communicate(timeout=5)
 check(p.returncode==1,'Combined duration above one hour is rejected')
 # An I/O failure in the pressure phase must preserve its baseline and publish FAILED.
 scratch=d/'workloads/scratch'
 scratch.rmdir()
 scratch.write_text('not a directory')
 p,rid=run(d,mode='disk',seconds=1)
 p.communicate(timeout=5); final=read(d)
 check(p.returncode==1 and final['State']=='FAILED' and final['Phase']=='disk','Workload failure is published after a successful baseline')
 check(final['Phases'][0]['Phase']=='baseline' and 'Error' in final['Phases'][1],'Failure retains prior phase results and its error')
print('TOTAL:',count,'sequence checks passed.')

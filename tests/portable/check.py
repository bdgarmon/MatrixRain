import subprocess,pathlib,json,time,uuid,tempfile,os
base=pathlib.Path(__file__).resolve().parent
host=base/'bin/Debug/net10.0/TestHost.dll'
passed=0
def check(b,n):
 global passed
 assert b,n
 passed+=1;print('PASS:',n,flush=True)
def launch(d,mode='baseline',secs=2,extra=()):
 rid=uuid.uuid4().hex
 return subprocess.Popen(['dotnet',str(host),'--data',str(d),'--mode',mode,'--seconds',str(secs),'--run-id',rid,'--threads','1','--intensity','10',*extra],stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True),rid
def await_run(d,rid):
 for _ in range(100):
  try:
   r=json.loads((d/'workloads/run.json').read_text())
   if r['RunId']==rid:return r
  except (FileNotFoundError,json.JSONDecodeError):pass
  time.sleep(.05)
 raise AssertionError('No startup acknowledgement')
with tempfile.TemporaryDirectory(prefix='Matrix integration ') as tmp:
 d=pathlib.Path(tmp)
 (d/'config.json').write_text(json.dumps({'StateFile':'nested/state.json'}))
 p,rid=launch(d,'cpu',10)
 r=await_run(d,rid);check(r['State']=='RUNNING','Process publishes startup acknowledgement')
 p2,rid2=launch(d);out,err=p2.communicate(timeout=5)
 check(p2.returncode==1,'Second process rejected while run active')
 check(json.loads((d/'workloads/run.json').read_text())['RunId']==rid,'Rejected run does not overwrite active state')
 (d/f'workloads/stop-{rid}').write_text('stop')
 p.communicate(timeout=6)
 check(p.returncode==0,'Stop exits worker cleanly')
 r=json.loads((d/'workloads/run.json').read_text());check(r['State']=='IDLE' and r['Result']['Reason']=='stopped by operator','Operator stop persists result')
 check(json.loads((d/'nested/state.json').read_text())['RunId']==rid,'Configured state path honored')
 check(not (d/f'workloads/stop-{rid}').exists(),'Consumed stop request removed')
 p,rid=launch(d,'science',1);p.communicate(timeout=5)
 r=json.loads((d/'workloads/run.json').read_text())
 check(p.returncode==0 and r['State']=='COMPLETED' and 2.5<r['Result']['Pi']<3.8,'Scientific result survives process/JSON boundary')
 p,rid=launch(d,'baseline',1);p.communicate(timeout=5)
 check(json.loads((d/'nested/state.json').read_text())['RainEnabled'] is False,'Baseline requests rain off')
 p,rid=launch(d,'visualization',1);p.communicate(timeout=5)
 check(json.loads((d/'nested/state.json').read_text())['RainEnabled'] is True,'Visualization requests rain on')
 p,rid=launch(d,'baseline',10);await_run(d,rid);p.kill();p.communicate(timeout=5)
 scratch=d/'workloads/scratch';scratch.mkdir(exist_ok=True)
 orphan=scratch/('io-'+uuid.uuid4().hex+'.tmp');orphan.write_bytes(b'x'*32)
 sentinel=scratch/'keep.txt';sentinel.write_text('preserve')
 p,rid=launch(d,'baseline',1);p.communicate(timeout=5)
 check(p.returncode==0,'Crash releases lease for next run')
 check(not orphan.exists() and sentinel.read_text()=='preserve','Recovery cleans named orphan scratch only')
 p,rid=launch(d,'cpu',10);await_run(d,rid)
 wrong=subprocess.run(['dotnet',str(host),'--data',str(d),'--stop',uuid.uuid4().hex,'--run-id',uuid.uuid4().hex],capture_output=True,timeout=5)
 check(wrong.returncode==0 and p.poll() is None,'Stale stop target cannot stop current run')
 stopped=subprocess.run(['dotnet',str(host),'--data',str(d),'--stop',rid,'--run-id',uuid.uuid4().hex],capture_output=True,timeout=5)
 p.communicate(timeout=5)
 check(stopped.returncode==0 and json.loads((d/'workloads/run.json').read_text())['State']=='IDLE','Control-panel stop action cancels targeted run')
 (d/'config.json').write_text('{broken')
 p,rid=launch(d,'cpu',1);p.communicate(timeout=5)
 check(p.returncode==1 and (d/f'workloads/error-{rid}.txt').exists(),'Startup errors available to unelevated panel by command ID')
 (d/'config.json').write_text(json.dumps({'StateFile':'nested/state.json'}))
 p,rid=launch(d,'invalid',1);p.communicate(timeout=5)
 check(p.returncode==1,'Invalid mode fails before running')
 for i in range(25): (d/f'workloads/result-{uuid.uuid4().hex}.json').write_text('{}')
 p,rid=launch(d,'baseline',1);p.communicate(timeout=5)
 check(len(list((d/'workloads').glob('result-*.json')))==20,'Result retention bounded at 20 files')
 (d/'config.json').write_text(json.dumps({'StateFile':''}))
 p,rid=launch(d,'cpu',1);p.communicate(timeout=5)
 check(p.returncode==1,'Disabled state integration rejects startup')
print('TOTAL:',passed,'process integration checks passed.')

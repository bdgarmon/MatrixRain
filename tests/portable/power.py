import pathlib,subprocess,tempfile,json,time,uuid
host=pathlib.Path(__file__).resolve().parent/'bin/Debug/net10.0/TestHost.dll'
count=0
def check(ok,label):
 global count
 assert ok,label
 count+=1;print('PASS:',label,flush=True)
with tempfile.TemporaryDirectory(prefix='Matrix power ') as folder:
 d=pathlib.Path(folder);rid=uuid.uuid4().hex
 worker=subprocess.Popen(['dotnet',str(host),'--data',str(d),'--run-id',rid,'--mode','cpu','--seconds','30','--intensity','10','--threads','1'],stdout=subprocess.PIPE,stderr=subprocess.PIPE)
 try:
  for _ in range(100):
   if (d/'workloads/run.json').exists():break
   time.sleep(.05)
  pid=json.loads((d/'workloads/run.json').read_text())['Pid']
  command=uuid.uuid4().hex
  p=subprocess.run(['dotnet',str(host),'--data',str(d),'--run-id',command,'--power-report','true'],capture_output=True,timeout=10)
  power=json.loads((d/f'workloads/power-{command}.json').read_text())
  check(p.returncode==0 and all(k in power for k in ['CapturedUtc','/requests','/getactivescheme','/a']),'Helper captures a dated fixed-command report')
  run=json.loads((d/'workloads/run.json').read_text())
  check(run['RunId']==rid and run['Pid']==pid and worker.poll() is None,'Power helper does not replace or stop the active workload')
  p=subprocess.run(['dotnet',str(host),'--data',str(d),'--power-report','invalid'],capture_output=True,timeout=10)
  check(p.returncode==1,'Invalid power request is rejected')
  for i in range(12): (d/f'workloads/power-{uuid.uuid4().hex}.json').write_text('{}')
  p=subprocess.run(['dotnet',str(host),'--data',str(d),'--power-report','true'],capture_output=True,timeout=10)
  check(p.returncode==0 and len(list((d/'workloads').glob('power-*.json')))==10,'Power report retention is bounded')
 finally:
  (d/f'workloads/stop-{rid}').write_text('stop');worker.communicate(timeout=10)
print('TOTAL:',count,'power process checks passed.')

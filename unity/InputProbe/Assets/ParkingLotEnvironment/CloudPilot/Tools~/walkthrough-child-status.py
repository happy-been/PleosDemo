"""Supervise the real Unity child, recording its OS exit before Xvfb cleanup.
A distinct process session isolates editor child-process shutdown from the caller.
No authentication/environment settings are changed.
"""
import json, os, signal, subprocess, sys, time, resource
from pathlib import Path
entry, log, status, *extra = sys.argv[1:]
editor='/home/codespace/Unity/Hub/Editor/2022.3.16f1/Editor/Unity'
project='/workspaces/parkside-source/unity/InputProbe'
cmd=[editor,'-batchmode','-force-glcore','-projectPath',project,'-executeMethod',entry,'-logFile',log] + extra
started=time.monotonic()
child=subprocess.Popen(cmd,start_new_session=True)
reason='normal_wait'
peak_rss_kb=0
try:
 while True:
  try:
   try:
    for line in Path(f'/proc/{child.pid}/status').read_text().splitlines():
     if line.startswith('VmHWM:'): peak_rss_kb=max(peak_rss_kb,int(line.split()[1]))
   except (FileNotFoundError, ProcessLookupError): pass
   result=child.wait(timeout=1)
   break
  except subprocess.TimeoutExpired:
   if Path('/workspaces/parkside-setup/STOP').exists() or time.monotonic()-started>1800:
    reason='STOP' if Path('/workspaces/parkside-setup/STOP').exists() else 'timeout_1800s'
    os.killpg(child.pid,signal.SIGTERM)
    try: result=child.wait(timeout=20)
    except subprocess.TimeoutExpired:
     os.killpg(child.pid,signal.SIGKILL);result=child.wait()
    break
finally:
 if child.poll() is not None:
  data={'unity_pid':child.pid,'unity_os_returncode':child.returncode,'reason':reason,'duration_seconds':round(time.monotonic()-started,2),'observed_unity_peak_rss_kb':peak_rss_kb,'wait4_child_maxrss_kb':resource.getrusage(resource.RUSAGE_CHILDREN).ru_maxrss,'entry':entry,'log':log}
  Path(status).write_text(json.dumps(data,indent=2)+'\n');print(json.dumps(data),flush=True)
sys.exit(result if result>=0 else 128-result)

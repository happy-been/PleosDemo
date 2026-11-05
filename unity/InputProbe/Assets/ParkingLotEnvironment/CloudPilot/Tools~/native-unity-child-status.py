"""Supervise the real Unity child, recording its OS exit before Xvfb cleanup.
A distinct process session isolates editor child-process shutdown from the caller.
No authentication/environment settings are changed.
"""
import json, os, signal, subprocess, sys, time
from pathlib import Path
entry, log, status = sys.argv[1:]
editor='/home/codespace/Unity/Hub/Editor/2022.3.16f1/Editor/Unity'
project='/workspaces/parkside-source/unity/InputProbe'
cmd=[editor,'-batchmode','-force-glcore','-projectPath',project,'-executeMethod',entry,'-logFile',log]
started=time.monotonic()
child=subprocess.Popen(cmd,start_new_session=True)
reason='normal_wait'
try:
 while True:
  try:
   result=child.wait(timeout=1)
   break
  except subprocess.TimeoutExpired:
   if Path('/workspaces/parkside-setup/STOP').exists() or time.monotonic()-started>780:
    reason='STOP' if Path('/workspaces/parkside-setup/STOP').exists() else 'timeout_780s'
    os.killpg(child.pid,signal.SIGTERM)
    try: result=child.wait(timeout=20)
    except subprocess.TimeoutExpired:
     os.killpg(child.pid,signal.SIGKILL);result=child.wait()
    break
finally:
 if child.poll() is not None:
  data={'unity_pid':child.pid,'unity_os_returncode':child.returncode,'reason':reason,'duration_seconds':round(time.monotonic()-started,2),'entry':entry,'log':log}
  Path(status).write_text(json.dumps(data,indent=2)+'\n');print(json.dumps(data),flush=True)
sys.exit(result if result>=0 else 128-result)

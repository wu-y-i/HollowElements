import time
from runtime_acceptance import command

command("load",slot=3)
deadline=time.monotonic()+25
while time.monotonic()<deadline:
    time.sleep(.5)
    status=command("status")
    if status["hero"] and status["scene"]!="Menu_Title":
        break
else:
    raise TimeoutError("load game")
time.sleep(2)
command("goto",scene="GG_Atrium",gate="door_dreamEnter")
deadline=time.monotonic()+25
while time.monotonic()<deadline:
    time.sleep(.5)
    if command("status")["scene"]=="GG_Atrium":
        break
else:
    raise TimeoutError("enter Godhome")
time.sleep(2)
command("ready")
command("position",target="hero",x=111,y=14.4)
command("face",right=True)
print("Godhome acceptance room ready; save writes suppressed.")

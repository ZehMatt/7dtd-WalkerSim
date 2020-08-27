# WalkerSim
Zombie walker simulation server plugin for 7 Days to Die

# What is this?
This plugin replaces the zombie spawning model with an offline simulation. Players on the server will be 
assigned a visibility zone that decides which zombies will be active and which ones will be offline. 
If the zombie was killed it will respawn at the map border as an offline version and roams the map, if the zombie 
is still alive but leaves the player visibility it will become offline but keeping attributes such as health
and class.

# How does it work?
The simulation will create a finite set of zombies based on the population size. The zombie agents will roam the map
by picking random zones of the configurable grid. Once a zombie reached the desired target zone it will pick a new zone
and keeps wandering. 

![Preview Image](Images/Simulation01.gif)  
Players will have a box around them, the size of the box is determined by the servers maximum view
configuration, if the zombie agent crosses into the players box border it will create a real in-game zombie, if the
zombie is out of the player zone it will be despawned and moved back into the simulation retaining its position and
attributes such as health and zombie class, if a zombie is killed the simulation will create a new agent near the border.

# Installation (DMT)
Download the latest 'DMT' release from https://github.com/ZehMatt/7dtd-WalkerSim/releases and extract the Mod folder into your DMT mods folder. Launch DMT and press build, that should be all.

# Installation (Dedicated Server)
Download the latest 'Dedicated' release from https://github.com/ZehMatt/7dtd-WalkerSim/releases and extract the Mod folder into your <7daystodiededicated_path>/Mods folder and start the server.

# Configuration
You are able to modify some aspects of the simulation via the WalkerSim.xml, this file has to exist in "<7daystodiededicated>/Mods/WalkerSim" for either the Dedicated or DMT variant. In order to reset the saved with the new configuration either start a new map or type "walkersim reset" into the dedicated server/game console.

# Inspecting the simulation
The plugin also provides a viewer client, the server must be required to enable this specifically as it consumes a rather big
amount of bandwidth, it is currently not optimized or pretty but but helps the development a lot.
![Preview Image](Images/Viewer01.png)

# Bugs
This is more a proof of concept than a fully fledged plugin at this point, so be aware.

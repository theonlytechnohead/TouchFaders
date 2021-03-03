# TouchFaders MIDI
![Screenshot of application](/Screenshot.png?raw=true "TouchFaders MIDI application")
## What is this?
The YAMAHA® LS9 digital mixing console for audio is a fantastically capable device. There exists three issues with it:

1. No DCA's is a very unfortunate omission, especially considering the M7CL series has them (and released a year earlier!)
1. Input-to-Matrix is not supported ☹ (again, M7CL getting all the love)
1. Only one remote device can control the desk at a time (and the only mobile device that can do so is an iPad, with the StageMix app from YAMAHA®)

Unfortunately, I can't easily fix 1 or 2, so instead I decided to focus my efforts on 3.  
*This program is the solution*

## How does it work?
It uses the MIDI (v1) protocol to communicate to the console, and then forwards appropriate data to OSC devices  
Requests received from OSC devices are also issued to the console on their behalf, as MIDI

## Instructions for use:
You need:
* A USB to MIDI adapter / cable/ interface (to make the PC talk to the console)
* A computer
* A wireless LAN network
* Mobile device(s) with the [TouchFaders mobile app](http://github.com/theonlytechnohead/TouchFaders_APP) installed (for Android only currently)
  * Conversely, you can create your own compatible app that uses my protocol, zerOSConf, for configuration, and OSC for mixing

Once that's all done, follow the below steps:
1. Connect the computer and console, using the MIDI ports on the console
1. Enable MIDI SysEx on the console
    1. In the __DISPLAY ACCESS__ section, press the __[SETUP]__ key repeatedly to access the __MISC SETUP__ screen
    1. Move the cursor to the __MIDI SETUP__ button, and press the __[ENTER]__ key to access the __MIDI SETUP__ window
    1. Use the __[INC]__ and __[DEC]__ keys or the __data wheel__ to change the *PORT/CH* field to __'MIDI'__ for both *Tx* and *Rx*
    1. Set an appropriate device ID (1 - 16) in the field below the port selection. Please use the same ID for both *Tx* and *Rx*
    1. Move the cursor to the *Tx* and *Rx* buttons, toggling each so that the __'Parameter Change'__ *Tx* and *Rx* buttons are lit, and all other *Tx*, *Rx*, and *ECHO* buttons are dark
1. Open the TouchFaders MIDI program on the computer
1. Select the appropriate MIDI ports from the 'MIDI input device' and 'MIDI outuput device' comboboxes
1. Click 'Start MIDI' (or press the 'S' key). The app will now sync with the console. Please wait for the progress bar to complete before trying to do too much other stuff!
1. Open the [TouchFaders mobile app](http://github.com/theonlytechnohead/TouchFaders_APP) on the mobile devices, and select the computer's hostname to initiate a connection.
	* If the automatic discovery isn't working, try restarting the app, by closing it completely, and reopening it. If this still does not find the computer, a manual connection via IPv4 address is possible, but you *might* want to check your network at that point...
1. On the [TouchFaders mobile app](http://github.com/theonlytechnohead/TouchFaders_APP), select the appropriate mix
	* If you select a mix, and something doesn't look right, press the 'O' key in TouchFaders MIDI to resync OSC devices
1. You're done!

## I want to do something similar to this, but I don't know how!
Check out the [Wiki](../../wiki)

## Todo
- [x] Add general config file for storing data about settings
- [x] Implement 'versioning' and settings for other config files
- [x] Add UI to adjust settings
- [x] Update 'Add device' dialog UI to be scalable, rearrange vertically
- [x] ~~Find the MIDI command for the 'selected channel' info~~ Does not exist 😢
  - [x] ~~Implement 'selected channel' status into main window UI~~
- [x] User configurable device ID
- [X] User configurable mixes (you need reconnect devices to update changes)
- [x] User configurable channels (as above)
- [x] User configurable channel linking, or getting channel link groups from the console
  - [ ] Communicate linked channel volumes on to remove devices, etc...
- [x] Support LS9-16
- [x] Channel configuration editor
 - [x] Channel names
 - [x] Channel link group
- [x] Selected channel information, take two
  - [x] Channel level
  - [x] Channel name (ASCII, once you correct the 7bit encoding of MIDI from 5bytes down to 4bytes + a nibble of 0's)
  - [x] Channel colour
  - [x] Channel icon
- [x] WinAPI audio session control via MIDI! (no name though, as pending above)
  - [x] Console -> PC
  - [x] PC -> Console
  - [x] fix stuff to avoid feedback loop, so that the SessionUI slider actually works properly (there were multiple levels of feedback, yikes)
- [x] zerOSConf implementation!
  - [x] update zerOSConf implementation for truly zeroconf operation
    - [x] rework and gut out the OSC device handling for more dynamic connections
    - [x] remove support for legacy clients
- [x] Mix metering data
  - [x] Get detailed data
  - [x] Broadcast detailed data
- [ ] rework all the MIDI code
  - [ ] QL5 support
    - [ ] QL1 support?
  - [ ] CL series?
    - [ ] Rivage?

## I'm interested in the code, how does it work? Can I re-use it?
It's fully open-source, look all you like.
You're welcome to clone it for a closer look, but please don't redistribute this code, or any modified code (except as a pull request to this repository)  
If you really like it and want to do more with it, go make your own project! Use this as the functional example I didn't have, and go make even something cooler  
That's not a legal licence, but also it's not like I can stop you anyway

## I can make this so much better
Please make a pull request when you do, otherwise suggestions via the 'Issues' tab are appreciated

---
GitHub repo for TouchFaders MIDI program, developed by Craig Anderson

"Yamaha" is a registered trademark of Yamaha Corporation.

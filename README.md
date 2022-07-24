# SteamOverlay plugin for Playnite

With this plugin you will be able to:
- Use steam overlay in any game
- Change the game that will be displayed in the overlay

For example, this is a non-steam Cuphead, with RDR2 overlay:
![Cuphead with RDR2 overlay](/Screenshots/CupheadOverlay.png)

The main difference with the [SpecialK Helper plugin](https://playnite.link/forum/thread-1162.html) is that you will not need to download additional software. This plugin will only inject the one dll needed to display the overlay.
Also, the plugin will not create a non-steam shortcut, instead it will directly add an overlay to the game.

## WARNING
- **Do not change or delete the play action of the plugin!**  
If you want to return the old play action, just disable the overlay through the menu.
- **Do not use the plugin in online competitive games that have an anti-cheat!**  
This can lead to a ban of your account, because the plugin injects a dll into the game, which can be seen as a cheat. Most likely the anti-cheat will simply not let you inject the dll, but just in case it is better not to try to use the overlay at all.

## How to install it?

1. [Download](https://github.com/IchinichiQ/SteamOverlay/releases/download/v0.1/SteamOverlay_eb3e6a5d-4bc1-4738-a328-cc62959750a1_0_1.pext) the latest version from releases.
2. Open the downloaded file.
3. Confirm the installation in the opened Playnite window.

## How to use it?

In the context menu of the game, select the item "Steam overlay", and click on the button "Enable Steam overlay" (or if the overlay is already enabled, instead it will be "Disable Steam overlay").  
Once enabled, the plugin will replace the game's play action with its own. The old play action will be saved, and after overlay disabling will be restored.  
Game playtime will not be saved in Steam.

## How it works?

After starting the game via Playnite, the injector creates a suspended game process, injects an overlay dll with the needed variables into it, and resumes the process. I tried to replicate Steam's behavior when launching the game, so basically the injector behaves like Steam.

## Features I want to add in the future

- Steam App ID search dialog by game name
- Dialog to select the play action, on the basis of which the plugin's play action will be created
- Support play actions with types other than file
- Automatic Steam directory search

## Screenshots
Plugin settings:  
![Plugin settings](/Screenshots/PluginSettings.png)

Game context menu item:  
![Context menu item](/Screenshots/MenuItem.png)

Game specific settings:  
![Context menu item](/Screenshots/GameSpecificSettings.png)
# Qbot

## What is it

A customized version of [patHyatt](https://github.com/patHyatt/)'s [XmppBot-for-HipChat](https://github.com/patHyatt/XmppBot-for-HipChat) that manages the queue for a scarce resource.

## To use:

Qbot responds to the following commands:

|command  |shortcut|result|
|---------|--------|------|
|!dibs    | !+     | Call dibs on the DAC|
|!release | !-     | Release the DAC or rescind a dibs |
|!steal   | !$     | Take the DAC from the current owner |
|!status  | !?     | Get the DAC queue status |
|!help    |        | Get DACbot commands |

## Installation

You can run the bot as a console application, or you can install it as a Windows Service by running: 

	XmppBot.Service.exe install

For more info about installing as a service, see the [TopShelf documentation](http://docs.topshelf-project.com/en/latest/overview/commandline.html).

## Issues 
If you have an issue or identify a bug, please [file an issue](https://github.com/macterra/DACbot/issues/new) or [create a pull request](https://github.com/macterra/DACbot/compare).

## License
[MIT](https://github.com/macterra/DACbot/blob/master/LICENSE.md)

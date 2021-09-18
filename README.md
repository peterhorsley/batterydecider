# Battery Decider
Uses Enphase Enlighten API to help decision around battery storage.

# How to use
Download the latest release (or build in Visual Studio) and run BatteryDecider.exe from a terminal to view help.

This program queries the Enphase API for a given solar system and
retrieves production and consumption data for a specifed date range.
Statistics are then computed including average daily export energy
to help in deciding whether adding a battery is warranted (with the
assumption being that it's best to self-consume rather than export).

Usage: BatteryDecider.exe [systemId] [key] [userId] [startDate] [endDate]

[systemId] - found in the URL for your enphase system e.g. /pv/systems/<systemId>
  
[key] - developer key, which you can get here https://developer.enphase.com/docs/quickstart.html
  
[userId] - found in your API settings at /pv/settings/<systemId>
  
[startDate] - YYYY-MM-DD
  
[endDate] - YYYY-MM-DD

This app will will make API calls to get production and consumption data for the date range, then will do some simple logic to calculate the following:

- average daily production
- average daily consumption
- average daily energy exported to grid
- average daily energy imported from grid

The last two are the most useful in deciding on a battery size, based on the assumption that if (exported energy > size of battery < imported energy) holds true, then a battery may make good sense, since the system will be able to charge it full and discharge it on average every day.

Tip: Although there are other factors to consider, you can compare your average daily imported to average daily exported above to gauge the size of battery you could add to reduce your imports. A battery with a size similar to your average daily exported energy would would allow you to import that much less energy from the grid.

private IMyBatteryBlock battery;
private float maxStoredPower = 0;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;

    battery = GridTerminalSystem.GetBlockWithName("Battery") as IMyBatteryBlock;
    maxStoredPower = battery.MaxStoredPower;
}

public void Main(string argument, UpdateType updateSource)
{
    Echo((battery.CurrentStoredPower / maxStoredPower).ToString());
}

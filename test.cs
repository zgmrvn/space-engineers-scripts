private IMyShipConnector connector;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;

    connector = GridTerminalSystem.GetBlockWithName("Connector Front") as IMyShipConnector;
    connector.Enabled = false;
}

public void Main(string argument, UpdateType updateSource)
{
    Echo(connector.Enabled.ToString());
}

private List<IMyTerminalBlock> containers;
private IMyTextPanel screen;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;

    containers = new List<IMyTerminalBlock>();
    screen = GridTerminalSystem.GetBlockWithName("Stock Ecran") as IMyTextPanel;
}

public void Main(string argument, UpdateType updateSource)
{
    (GridTerminalSystem.GetBlockGroupWithName("Stock Lingots") as IMyBlockGroup).GetBlocks(containers);

    // Clear screens.
    screen.WriteText("", false);

    // Update screens.
    for (int i = 0; i < containers.Count; i++)
    {
        IMyCargoContainer container = containers[i] as IMyCargoContainer;

        // Get inventory.
        IMyInventory inventory = container.GetInventory(0);

        // Get item type.
        var items = new List<MyInventoryItem>();
        inventory.GetItems(items);

        if (items.Count == 0)
        {
            continue;
        }

        // Prepare strings.
        string type = items[0].Type.ToString().Split('/')[1];
        string tons = ((float)inventory.CurrentMass / 1000).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " t";

        // Write on screens.
        screen.WriteText(type + " : " + tons + "\n", true);

        Echo(type  + " : " + tons);
    }
}

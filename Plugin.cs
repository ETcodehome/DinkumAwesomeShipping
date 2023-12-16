﻿using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using HarmonyLib;
using System;
using System.Collections.Generic; // for list

namespace AwesomeShipping;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{

    public const string CONFIG_GENERAL              = "General";
    public const int ROTATION_FACING_SOUTH          = 1;
    public const int ROTATION_FACING_WEST           = 2;
    public const int ROTATION_FACING_NORTH          = 3;
    public const int ROTATION_FACING_EAST           = 4;
    public const int SHOPID_JOHNS                   = 8;
    public const int LICENSE_ID_COMMERCE            = 8;
    public const int NPC_ID_JOHN                    = 2;

    /// <summary>
    /// Harmony patcher
    /// </summary>
    private readonly Harmony _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
    public static bool _showNotifications           = false;                                  
    public static bool _dinksAsItem                 = true;
    public static int _searchDistance               = 1;                                  
    public static string _excludeTileIDs            = "210";
    public static List<int> parsedExcludeIDs;

    /// <summary>
    /// Plugin constructor - run when the plugin is first generated
    /// </summary>
    public Plugin()
    {
        _searchDistance = base.Config.Bind<int>(CONFIG_GENERAL, "SearchDistance", 1, "Number of tiles in each direction around Johns shop to sell container contents from.").Value;
        _dinksAsItem = base.Config.Bind<bool>(CONFIG_GENERAL, "DinksAsItem", true, "When enabled dinks will be deposited back into the sale chest as a physical item. If disabled dinks go direct to wallet.").Value;
        _excludeTileIDs = base.Config.Bind<string>(CONFIG_GENERAL, "ExcludeTileIDs", "210", "Comma separated list of tile object IDs to exclude from processing ie 210,425,426.").Value;
        parsedExcludeIDs = ParseStringToIntArray(_excludeTileIDs);
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} constructor triggered.");
    }

    /// <summary>
    /// Triggered when the plugin is activated (not constructed)
    /// </summary>
    private void Awake()
    {
        ApplyHarmonyPatches();
    }


    /// <summary>
    /// Patches native game routines with all harmony patches declared.
    /// </summary>
    private void ApplyHarmonyPatches()
    {
        _harmony.PatchAll();
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} finished merging harmony patches!");
    }


    /// <summary>
    /// Returns the name of a multitile object at a location
    /// </summary>
    public static string GetBuildingNameAt(int x, int z){
        WorldManager WM = WorldManager.Instance;
        int multiTileID = WM.onTileMap[x,z];
        return BuildingManager.manage.getBuildingName(multiTileID);
    }


    /// <summary>
    /// BepInEx plugins dont seem to accept arrays as config value types.
    /// To deal with this, take a comma seperated string and parse it back to an array of ints
    /// </summary>
    public List<int> ParseStringToIntArray(string input){
        string[] words = input.Split(',');
        List<int> result = new List<int>();
        foreach(string id in words){
            try{
                result.Add(Int32.Parse(id));
            }
            catch (FormatException)
            {
                Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} - " + input + " is malformed.");
                // do nothing deliberately - don't let the exception propagate higher
            }
        }
        return result;
    }



    /// <summary>
    /// Returns the rotation adjusted endX and endZ for an arbitrary multitile object at x, z
    /// </summary>
    public static Vector2Int GetMultiTileObjectEnds(int x, int z){

        // retrieve all the values involved in the calculation
        WorldManager WM = WorldManager.Instance;
        int rotation = WM.rotationMap[x, z];

        try {
            TileObjectSettings settings = WM.getTileObjectSettings(x, z);
            int width = settings.xSize;
            int height = settings.ySize;

            // we invert the values if the building is oriented facing east or west
            if (rotation == ROTATION_FACING_WEST || rotation == ROTATION_FACING_EAST) {
                width = settings.ySize;
                height = settings.xSize;
            }

            // we minus 1 here because the base size is 1x1 so if we add it the resulting size is larger than the real footprint
            int endX = x + width - 1;
            int endZ = z + height - 1;

            return new Vector2Int(endX, endZ);
        } catch {
            return new Vector2Int(x, z);
        }

    }


    /// <summary>
    /// Searches for a tile with type matching the provided ID
    /// If provided an ID with multiple instances, returns the first instance it finds only.
    /// Returns -1,-1 coords if no instance found.
    /// </summary>
    public static Vector2Int FindTileByID(int searchID){
        int mX = -1;
        int mZ = -1;
        WorldManager WM = WorldManager.Instance;
        int defaultMapSize = 1000;
        for (int xTile = 0; xTile < defaultMapSize; xTile++){
            for (int zTile = 0; zTile <= defaultMapSize; zTile++){
                try{
                    if (WM.onTileMap[xTile, zTile] == searchID){
                        mX = xTile;
                        mZ = zTile;
                        return new Vector2Int(mX, mZ);
                    }
                } catch {
                    // do nothing but catch index out of range errors.
                }
            }
        }
        return new Vector2Int(mX, mZ);
    }


    /// <summary>
    /// Very slightly reworked copy of GiveNPC.checkSellSlotForTask to use an arbitrary ID rather than an inv slot as it's parameter
    /// Verbatim otherwise.
    /// </summary>
    /// <param name="itemID"></param>
    /// <param name="stackAmount"></param>
    public static void UpdateSellTasks(int itemID, int stackAmount)
	{
		if (Inventory.Instance.allItems[itemID].taskWhenSold != DailyTaskGenerator.genericTaskType.None)
		{
			DailyTaskGenerator.generate.doATask(Inventory.Instance.allItems[itemID].taskWhenSold, stackAmount);
			return;
		}
		if (Inventory.Instance.allItems[itemID].consumeable)
		{
			if (Inventory.Instance.allItems[itemID].consumeable.isFruit)
			{
				DailyTaskGenerator.generate.doATask(DailyTaskGenerator.genericTaskType.SellFruit, stackAmount);
			}
			else if (Inventory.Instance.allItems[itemID].consumeable.isVegitable)
			{
				DailyTaskGenerator.generate.doATask(DailyTaskGenerator.genericTaskType.SellCrops, stackAmount);
			}
		}
		if (Inventory.Instance.allItems[itemID].fish)
		{
			DailyTaskGenerator.generate.doATask(DailyTaskGenerator.genericTaskType.SellFish, stackAmount);
		}
		if (Inventory.Instance.allItems[itemID].bug)
		{
			DailyTaskGenerator.generate.doATask(DailyTaskGenerator.genericTaskType.SellBugs, stackAmount);
		}
	}


    /// <summary>
    /// Sells all the contents of the supplied chest as though the game host walked in and pushed them across the counter to John
    /// Doesn't yield reward tickets
    /// </summary>
    /// <param name="chest"></param>
    private static void SellShippingCrateContentsToJohn(Chest chest){

        try {

            int[] itemIDs = chest.itemIds;
            int[] itemStacks = chest.itemStacks;
            int maxInvSlots = 24;
            int saleTotal = 0;
            int dinkSlot = -1;
            int ITEMID_DINK = 299;

            // Guard against empty chests
            if (itemIDs == null){
                return;
            }

            // Process each inventory slot we're selling
            for (int i = 0; i < maxInvSlots; i++){
                
                // tem in the inventory slot we are processing
                int itemID = itemIDs[i];

                // guard clause - don't sell dinks
                if (itemID == ITEMID_DINK){
                    dinkSlot = i;
                    continue;
                }

                // Guard clause - empty slot, go to next
                if (itemID < 0){
                    continue;
                }

                // Guard clause - avoid selling irreplacable items
                bool isVeryUnique = Inventory.Instance.allItems[itemID].isOneOfKindUniqueItem;
                bool isDeed = Inventory.Instance.allItems[itemID].isDeed;
                if (isVeryUnique || isDeed){
                    continue;
                }

                // Calculate sell quantity (stack size aware)
                bool unstackableItem = !Inventory.Instance.allItems[itemID].checkIfStackable();
                int sellQty = itemStacks[i];
                if (unstackableItem){
                    sellQty = 1;
                }

                // In case this is only slot that we can put the dinks back, update the slot ID if it hasn't been already
                if (dinkSlot == -1){
                    dinkSlot = i;
                }

                // calculate the sale outcomes
                int unitPrice = Inventory.Instance.allItems[itemID].value;
                int stackPrice = unitPrice * sellQty;

                // Track the sale metrics
                CharLevelManager.manage.addToDayTally(itemID, sellQty, CharLevelManager.manage.skillBoxes.Length);
                UpdateSellTasks(itemID, sellQty);
                saleTotal += stackPrice;

                // Remove the items shipped
                chest.itemIds[i] = -1;
                chest.itemStacks[i] = 0;

            }

            // Handle the currency 
            // Gotta consider commerce levels for mod to be viable for the min maxers out there
            int commerceLevel = LicenceManager.manage.allLicences[LICENSE_ID_COMMERCE].getCurrentLevel();
            float saleBonusPerLevel = 0.05f; // 5%
            int bonusDinks = (int)(commerceLevel * saleBonusPerLevel * saleTotal);
            saleTotal += bonusDinks;

            // Update metrics for money spent at Johns store
            NPCManager.manage.npcStatus[NPC_ID_JOHN].moneySpentAtStore += saleTotal;

            // Make a fat stack of dinks as a physical item or pay into wallet depending on setting
            // More fun as a physical item
            if (_dinksAsItem){
                chest.itemIds[dinkSlot] = ITEMID_DINK;
                chest.itemStacks[dinkSlot] += saleTotal;
            } else {
                Inventory.Instance.changeWallet(saleTotal, false);
            }

            // Update any related tasks
            CharLevelManager.manage.todaysMoneyTotal += saleTotal;
            DailyTaskGenerator.generate.doATask(DailyTaskGenerator.genericTaskType.SellItems, saleTotal);
        
        } catch {
            // don't process more
        }
        
    }


    private static void ShipAllShippingCrateContents(){       

        // Find Johns Store
        Vector2Int storePos = FindTileByID(SHOPID_JOHNS);

        // Guard clause - store wasn't found (probably not placed yet)
        if (storePos[0] < 0 || storePos[1] < 0){
            return;
        }
        
        // Calculate the full footprint extent of the store (rotation aware)
        Vector2Int ends = GetMultiTileObjectEnds(storePos[0], storePos[1]);
        
        // Extend the resulting region so that chests can be placed adjacent
        ends[0] += _searchDistance;
        ends[1] += _searchDistance;
        storePos[0] -= _searchDistance;
        storePos[1] -= _searchDistance;

        // Search the region for openable chests / cupboards etc
        // Get contianer contents if there is one there and sell it's contents
        for (int xTile = storePos[0]; xTile <= ends[0]; xTile++){
            for (int zTile = storePos[1]; zTile <= ends[1]; zTile++){
                Chest targetChest = ContainerManager.manage.getChestForWindow(xTile, zTile, null);
                if (targetChest != null){
                    int tileID = WorldManager.Instance.onTileMap[xTile,zTile];
                    if (!parsedExcludeIDs.Contains(tileID)){
                        SellShippingCrateContentsToJohn(targetChest);    
                    }
                }
            }   
        }
    }


    /// <summary>
    /// Patch to trigger shipping crate sale at end of each day 
    /// We do it before tallys are calculated so that items sold are included
    /// </summary>
    [HarmonyPatch(typeof(WorldManager), nameof(WorldManager.nextDay))]
    class PatchSellShippingCrateContents
    {
        private static void Prefix()
        {
            // Only the host should ever process shipping crates
            if (NetworkMapSharer.Instance.isServer){
                ShipAllShippingCrateContents();
            }
        }
    }

}

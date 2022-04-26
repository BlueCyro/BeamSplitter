using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using BaseX;
using System;
using System.Reflection.Emit;
using System.Reflection;



namespace BeamSplitter;
public class BeamSplitter : NeosMod
{
    public override string Author => "Cyro";
    public override string Name => "BeamSplitter";
    public override string Version => "1.0.0";
    
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<color> UserspaceInteractionColor = new ModConfigurationKey<color>("UserspaceInteractionColor", "Color used for the interaction laser in user space", () => color.Purple);
    
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<color> UserspaceInteractTouchColor = new ModConfigurationKey<color>("UserspaceInteractTouchColor", "Color used for the interaction laser in user space when touched", () => color.Magenta);
    
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<color> WorldspaceInteractionColor = new ModConfigurationKey<color>("WorldspaceInteractionColor", "Color used for the interaction laser in world space", () => color.Cyan);

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<color> WorldspaceInteractTouchColor = new ModConfigurationKey<color>("WorldspaceInteractTouchColor", "Color used for the interaction laser in world space when touched", () => color.Yellow);
    
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<color> NormalInteractionColor = new ModConfigurationKey<color>("NormalInteractionColor", "Color used for the laser when interacting with a non-grabbable, yet interactable object (like logix wires)", () => color.Green);
    
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<color> EditModeColor = new ModConfigurationKey<color>("EditModeColor", "Color used for the laser when in edit mode", () => color.Orange);

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<color> GrabColor = new ModConfigurationKey<color>("GrabColor", "Color used for the laser when interacting with a grabbable object", () => color.Red);

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<color> GrabFreeAxisColor = new ModConfigurationKey<color>("GrabFreeAxisCOlor", "Color used for the laser when your laser's axis is unconstrained when grabbing an object", () => color.Orange);
    public static ModConfiguration? Config;
    public override void OnEngineInit()
    {   
        UniLog.Log("BeamSplitter: OnEngineInit");
        Config = GetConfiguration();
        if (Config == null)
        {
            UniLog.Log("BeamSplitter: Config is null");
            return;
        }
        Config.Save(true);
        Harmony harmony = new Harmony("net.Cyro.BeamSplitter");
        harmony.PatchAll();
    }

    public static color UserspaceInteraction
    {
        get
        {
            return MathX.Clamp(Config!.GetValue(UserspaceInteractionColor), 0f, 3f);
        }
    }

    public static color UserspaceInteractTouch
    {
        get
        {
            return MathX.Clamp(Config!.GetValue(UserspaceInteractTouchColor), 0f, 3f);
        }
    }

    public static color WorldspaceInteraction
    {
        get
        {
            return MathX.Clamp(Config!.GetValue(WorldspaceInteractionColor), 0f, 3f);
        }
    }

    public static color WorldspaceInteractTouch
    {
        get
        {
            return MathX.Clamp(Config!.GetValue(WorldspaceInteractTouchColor), 0f, 3f);
        }
    }

    public static color NormalInteraction
    {
        get
        {
            return MathX.Clamp(Config!.GetValue(NormalInteractionColor), 0f, 3f);
        }
    }

    public static color EditMode
    {
        get
        {
            return MathX.Clamp(Config!.GetValue(EditModeColor), 0f, 3f);
        }
    }

    public static color Grab
    {
        get
        {
            return MathX.Clamp(Config!.GetValue(GrabColor), 0f, 3f);
        }
    }

    public static color GrabFreeAxis
    {
        get
        {
            return MathX.Clamp(Config!.GetValue(GrabFreeAxisColor), 0f, 3f);
        }
    }

    [HarmonyPatch(typeof(InteractionLaser))]
    public static class InteractionLaser_Color_Patch
    {
        [HarmonyPatch("UpdateLaser")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UpdateLaser_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var PropertyGetters = typeof(BeamSplitter).GetProperties(BindingFlags.Public | BindingFlags.Static).Select(p => p.GetGetMethod()).ToList();
            var codes = instructions.ToList();
            FieldInfo TargetField = AccessTools.Field(typeof(InteractionLaser), "_hitColor");
            MethodInfo setValueSyncField = AccessTools.PropertySetter(typeof(SyncField<>).MakeGenericType(typeof(color)), "Value");

            var foundCodes = codes.Select((code, index) => new { code, index }).Where(x => x.code.opcode == OpCodes.Ldfld && (FieldInfo)x.code.operand == TargetField).Select(x => x.index).ToList();
            List<Tuple<int, int>> setCodes = foundCodes.Select(x =>
            {
                int setValueIndex = -1;

                for (int i = x + 1; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldfld && (FieldInfo)codes[i].operand == TargetField)
                        break;
                    
                    if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == setValueSyncField)
                    {
                        setValueIndex = i;
                        break;
                    }
                }
                return new Tuple<int, int>(x, setValueIndex);
            }).ToList();

            // Userspace color pattern
            Dictionary<MethodInfo, int> UserspaceColorGetters = new Dictionary<MethodInfo, int>()
            {
                {AccessTools.PropertyGetter(typeof(color), "Purple"), 0},
                {AccessTools.PropertyGetter(typeof(color), "Magenta"), 0},
            };
            // Worldspace color pattern
            Dictionary<MethodInfo, int> WorldspaceColorGetters = new Dictionary<MethodInfo, int>()
            {
                {AccessTools.PropertyGetter(typeof(color), "Cyan"), 0},
                {AccessTools.PropertyGetter(typeof(color), "Yellow"), 0},
            };
            // Normal color pattern
            MethodInfo NormalColorGetter = AccessTools.PropertyGetter(typeof(color), "Green");

            // Edit mode color pattern
            MethodInfo EditModeColorGetter = AccessTools.PropertyGetter(typeof(color), "Orange");

            // Grab mode color pattern
            Dictionary<MethodInfo, int> GrabModeColorGetters = new Dictionary<MethodInfo, int>()
            {
                {AccessTools.PropertyGetter(typeof(color), "Red"), 0},
                {AccessTools.PropertyGetter(typeof(color), "Orange"), 0},
            };

            // Get all of the userspace color getters
            foreach(var code in setCodes)
            {
                for (int i = code.Item1; i < code.Item2; i++)
                {
                    if (codes[i].operand is MethodInfo method && UserspaceColorGetters.ContainsKey(method))
                    {
                        int val = UserspaceColorGetters[method];
                        UserspaceColorGetters[method] = val == 0 ? i : 0;
                    }
                }

                // Set the values to the correct ones
                // Check if all of the dictionary entries are greater than 0
                if (UserspaceColorGetters.Values.All(x => x > 0))
                {
                    UniLog.Log("BeamSplitter: UserspaceColorGetters all greater than 0");
                    for (int i = 0; i < UserspaceColorGetters.Count; i++)
                    {
                        if (UserspaceColorGetters[UserspaceColorGetters.Keys.ElementAt(i)] > 0)
                        {
                            codes[UserspaceColorGetters[UserspaceColorGetters.Keys.ElementAt(i)]].operand = PropertyGetters[i];
                        }
                    }
                    break;
                }
                else
                {
                    UniLog.Log("BeamSplitter: UserspaceColorGetters not all greater than 0");
                    // Set them all back to 0
                    foreach (var entry in UserspaceColorGetters.ToList())
                    {
                        UserspaceColorGetters[entry.Key] = 0;
                    }
                }
            }

            // Same for the world version
            foreach(var code in setCodes)
            {
                for (int i = code.Item1; i < code.Item2; i++)
                {
                    if (codes[i].operand is MethodInfo method && WorldspaceColorGetters.ContainsKey(method))
                    {
                        UniLog.Log("BeamSplitter: WorldspaceColorGetters contains method");
                        int val = WorldspaceColorGetters[method];
                        WorldspaceColorGetters[method] = val == 0 ? i : 0;
                    }
                }
                UniLog.Log("BeamSplitter: Checking if WorldspaceColorGetters all greater than 0");
                // Set the values to the correct ones
                if (WorldspaceColorGetters.Values.All(x => x > 0))
                {
                    UniLog.Log("BeamSplitter: WorldspaceColorGetters all greater than 0");
                    for (int i = 0; i < WorldspaceColorGetters.Count; i++)
                    {
                        if (WorldspaceColorGetters[WorldspaceColorGetters.Keys.ElementAt(i)] > 0)
                        {
                            UniLog.Log("BeamSplitter: Setting WorldspaceColorGetters");
                            codes[WorldspaceColorGetters[WorldspaceColorGetters.Keys.ElementAt(i)]].operand = PropertyGetters[i + 2];
                        }
                    }
                    break;
                }
                // Check if all of the dictionary entries are greater than 0
                else
                {
                    UniLog.Log("BeamSplitter: WorldspaceColorGetters not all greater than 0");
                    // Set them all back to 0
                    foreach (var entry in WorldspaceColorGetters.ToList())
                    {
                        UniLog.Log("BeamSplitter: Setting WorldspaceColorGetters to 0");
                        WorldspaceColorGetters[entry.Key] = 0;
                    }
                }
            }

            // Normal interaction color getter (for logix wires and stuff)
            foreach(var code in setCodes)
            {
                for (int i = code.Item1; i < code.Item2; i++)
                {
                    if (codes[i].operand is MethodInfo method && NormalColorGetter == method)
                    {
                        UniLog.Log("BeamSplitter: NormalColorGetter found");
                        codes[i].operand = PropertyGetters[4];
                    }
                }
            }
            // Edit mode color getter
            foreach(var code in setCodes)
            {
                List<int> found = new List<int>();
                for (int i = code.Item1; i < code.Item2; i++)
                {
                    if (codes[i].operand is MethodInfo method && method.ReturnType == typeof(color))
                    {
                        found.Add(i);
                    }
                }
                if (found.Count == 1 && (MethodInfo)codes[found[0]].operand == EditModeColorGetter)
                {
                    codes[found[0]].operand = PropertyGetters[5];
                }
            }

            // For the grab mode colors
            foreach(var code in setCodes)
            {
                List<int> found = new List<int>();
                for (int i = code.Item1; i < code.Item2; i++)
                {
                    if (codes[i].operand is MethodInfo method && GrabModeColorGetters.ContainsKey(method))
                    {
                        int val = GrabModeColorGetters[method];
                        GrabModeColorGetters[method] = val == 0 ? i : 0;
                    }
                }
                // Set the values to the correct ones
                if (GrabModeColorGetters.Values.All(x => x > 0))
                {
                    for (int i = 0; i < GrabModeColorGetters.Count; i++)
                    {
                        if (GrabModeColorGetters[GrabModeColorGetters.Keys.ElementAt(i)] > 0)
                        {
                            codes[GrabModeColorGetters[GrabModeColorGetters.Keys.ElementAt(i)]].operand = PropertyGetters[i + 6];
                        }
                    }
                    break;
                }
                else
                {
                    // Set them all back to 0
                    foreach (var entry in GrabModeColorGetters.ToList())
                    {
                        GrabModeColorGetters[entry.Key] = 0;
                    }
                }
            }
            return codes;
        }
    }
}

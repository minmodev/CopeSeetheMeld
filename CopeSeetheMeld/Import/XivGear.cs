using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CopeSeetheMeld.Import;

public partial class Import
{
    public record class XGSetCollection
    {
        public string? name;
        public List<XGSet>? sets;
    }

    public record class XGSet
    {
        public string? name;
        public Dictionary<string, XGItem>? items;
        public bool isSeparator;
    }

    public record class XGItem
    {
        public uint id;
        public List<XGId>? materia;
    }

    public record class XGId
    {
        public int id;
    }

    private async System.Threading.Tasks.Task ImportXIVG(string shortcode, string gearIndex)
    {
        var ix = gearIndex.Length == 0 ? -1 : int.Parse(gearIndex);
        var contents = await client.GetStringAsync($"https://api.xivgear.app/shortlink/{shortcode}");
        ImportXIVGJson(contents, ix);
    }

    // accepts a sheet ({"sets": [...]}) or a bare set ({"items": {...}})
    private void ImportXIVGJson(string contents, int ix)
    {
        using var doc = JsonDocument.Parse(contents);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new Exception("Not a valid xivgear JSON object");

        XGSet xgs;
        if (doc.RootElement.TryGetProperty("sets", out _))
        {
            var sheet = doc.RootElement.Deserialize<XGSetCollection>(jop) ?? throw new Exception("Bad xivgear sheet");
            var sets = sheet.sets ?? throw new Exception("Sheet has no sets");

            if (ix >= 0)
            {
                if (ix >= sets.Count)
                    throw new Exception($"Set index {ix} out of range (sheet has {sets.Count} sets)");
                xgs = sets[ix];
            }
            else
            {
                // no index given: take the first real set
                xgs = sets.FirstOrDefault(s => !s.isSeparator && s.items is { Count: > 0 })
                    ?? throw new Exception("Sheet contains no gearsets");
            }
        }
        else if (doc.RootElement.TryGetProperty("items", out _))
        {
            xgs = doc.RootElement.Deserialize<XGSet>(jop) ?? throw new Exception("Bad xivgear set");
        }
        else
        {
            throw new Exception("JSON is neither an xivgear sheet nor a set");
        }

        if (xgs.isSeparator || xgs.items == null)
            throw new Exception("Selected set has no items (is it a separator?)");

        var items = xgs.items;
        var gs = new Gearset(xgs.name ?? "xivgear import");

        void mk(ItemType ty, string field)
        {
            if (!items.TryGetValue(field, out var item))
                return;

            var row = Data.Item(item.id);

            var slot = new ItemSlot(item.id, ty, row.CanBeHq);
            var materia = item.materia ?? [];
            for (var i = 0; i < Math.Min(5, materia.Count); i++)
                if (materia[i].id >= 0)
                    slot.Materia[i] = (uint)materia[i].id;

            gs.Items.Add(slot);
        }

        mk(ItemType.Weapon, "Weapon");
        mk(ItemType.Head, "Head");
        mk(ItemType.Body, "Body");
        mk(ItemType.Hands, "Hand");
        mk(ItemType.Legs, "Legs");
        mk(ItemType.Feet, "Feet");
        mk(ItemType.Offhand, "OffHand");
        mk(ItemType.Ears, "Ears");
        mk(ItemType.Neck, "Neck");
        mk(ItemType.Wrists, "Wrist");
        mk(ItemType.Ring, "RingLeft");
        mk(ItemType.Ring, "RingRight");

        Plugin.Config.GearsetList.Add(gs);
    }
}

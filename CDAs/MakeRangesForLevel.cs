#region Using Directives

using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PrecisionMining.Common.Design;
using PrecisionMining.Common.Design.Data;
using PrecisionMining.Common.Math;
using PrecisionMining.Common.UI.Design;
using PrecisionMining.Spry.UI.Design;
using PrecisionMining.Common.Units;
using PrecisionMining.Spry;
using PrecisionMining.Spry.Util;
using PrecisionMining.Spry.Data;
using PrecisionMining.Spry.Design;
using PrecisionMining.Spry.Scenarios;
using PrecisionMining.Spry.Scenarios.Haulage;
using PrecisionMining.Spry.Scenarios.Scheduling;
using PrecisionMining.Spry.Scripting;
using PrecisionMining.Spry.Spreadsheets;
using PrecisionMining.Spry.Util.OptionsForm;
using PrecisionMining.Spry.Util.UI;

#endregion

//v2 add new chains into existing range dependency
public partial class RangesFilterByField
{

    #region Methods

    [CustomDesignAction]
    internal static CustomDesignAction CreateRangesByFieldValue()
    {
        var customDesignAction = new CustomDesignAction("Create Ranges By Field Value", "", "Ranges", Array.Empty<Keys>());
        customDesignAction.Visible = DesignButtonVisibility.Animation;
        customDesignAction.OptionsForm += (s, e) =>
        {
            var form = OptionsForm.Create("test");
            var fieldOption = form.Options.AddFieldSelect("Field", false);
            fieldOption.Table = customDesignAction.ActiveCase.SourceTable;
            fieldOption.RestoreValue("Create Ranges By Field Value Field", x => x.FullName, x => fieldOption.Table.Schema.GetField(x), true, true);
            var rangeOption = form.Options.AddRangeSelect("Filter Range");
            rangeOption.Table = customDesignAction.ActiveCase.SourceTable;
            rangeOption.RestoreValue("Create Ranges By Field Value Field", x => x.FullName, x => rangeOption.Table.Ranges.GetRange(x), true, true);

            e.OptionsForm = form;
        };
        customDesignAction.ApplyAction += (s, e) =>
        {
            var fieldOption = customDesignAction.ActionSettings[0] as FieldSelectOption;
            var field = fieldOption.Value;
            var rangeOption = customDesignAction.ActionSettings[1] as RangeSelectOption;
            var range = rangeOption.Value;
            var fieldLevels = new FieldLevels(customDesignAction.ActiveCase.SourceTable, null);
            fieldLevels.Add(field);
            var table = customDesignAction.ActiveCase.SourceTable;
            var root = NodeFieldGroupingBuilder.BuildTree(fieldLevels, FieldOffsetDependency.FIELDOFFSETDEBUG, range);

            var folder = "Fields\\" + field.Name + "\\";
            foreach (var item in root.Children)
            {
                var newRange = table.Ranges.GetOrCreateTextRange(folder + item.Key.PositionValue);
                var isInRange = range == null ? "" : "In Range: " + range.FullName + "\n";
                newRange.RangeText = isInRange + "*";
                var fieldCodeName = field.FullName.Replace("\\", "").Replace(" ", "");
                newRange.FilterExpression = string.Format("GetText({0}) == \"{1}\"", fieldCodeName, item.Key.PositionValue);
                e.Completed = true;
            }
        };
        return customDesignAction;
    }

    #endregion

}

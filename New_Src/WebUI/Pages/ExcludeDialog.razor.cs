using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Collections.Generic;
using System.Linq;

namespace WebUI.Pages
{
    // اضافه شدن پراپرتی Value برای نمایش در ستون دوم
    public class ExcludeItemModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public bool IsExcluded { get; set; }
    }

    public class ExcludeResult
    {
        public List<ExcludeItemModel> ExcludedItems { get; set; } = new();
        public string Reason { get; set; } = string.Empty;
    }

    public partial class ExcludeDialog
    {
        [CascadingParameter]
        IMudDialogInstance MudDialog { get; set; } = default!;

        [Parameter]
        public List<ExcludeItemModel> Items { get; set; } = new();

        public string ExcludeReason { get; set; } = string.Empty;

        private void Submit()
        {
            var resultData = new ExcludeResult
            {
                ExcludedItems = Items?.Where(x => x.IsExcluded).ToList() ?? new List<ExcludeItemModel>(),
                Reason = ExcludeReason
            };

            MudDialog.Close(DialogResult.Ok(resultData));
        }

        private void Cancel()
        {
            MudDialog.Cancel();
        }
    }
}
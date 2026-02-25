using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebUI.Pages
{
    public class CrmSelectionModel
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool IsIncluded { get; set; }
    }

    public partial class SelectCrmsDialog
    {
        [CascadingParameter]
        IMudDialogInstance MudDialog { get; set; } = default!;

        [Parameter]
        public List<CrmSelectionModel> Items { get; set; } = new();

        [Parameter]
        public EventCallback OnSelectionChanged { get; set; }

        private async Task OnCheckboxChanged(CrmSelectionModel item, ChangeEventArgs e)
        {
            item.IsIncluded = (bool)(e.Value ?? false);
            await NotifyChangeAsync();
        }

        private async Task SelectAll()
        {
            if (Items != null)
            {
                foreach (var item in Items) item.IsIncluded = true;
                await NotifyChangeAsync();
            }
        }

        private async Task DeselectAll()
        {
            if (Items != null)
            {
                foreach (var item in Items) item.IsIncluded = false;
                await NotifyChangeAsync();
            }
        }

        private async Task NotifyChangeAsync()
        {
            if (OnSelectionChanged.HasDelegate)
            {
                await OnSelectionChanged.InvokeAsync();
            }
        }

        private void Close()
        {
            MudDialog.Close(DialogResult.Ok(Items));
        }
    }
}
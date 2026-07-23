using UnityEngine;

namespace MemoAnchor.UI
{
    public partial class MainView
    {
        private void LoadInitialData()
        {
            if (MainInitialData.TryTake(out ScanMapListResponse mapResponse, out MemoListResponse memoResponse))
            {
                ApplyMapListResponse(mapResponse);
                ApplyMemoListResponse(memoResponse);
                return;
            }

            _ = LoadInitialDataAsync();
        }

        private async Awaitable LoadInitialDataAsync()
        {
            LoadingSpinnerController.ShowOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            try
            {
                await RefreshMapListAsync();
                await RefreshMemoListAsync();
            }
            finally
            {
                LoadingSpinnerController.HideOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            }
        }
    }
}

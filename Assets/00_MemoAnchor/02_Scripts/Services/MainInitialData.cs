using UnityEngine;

namespace MemoAnchor
{
    public static class MainInitialData
    {
        private static ScanMapListResponse mapList;
        private static MemoListResponse memoList;
        private static bool hasData;

        public static async Awaitable PreloadAsync()
        {
            var mapService = new ScanMapService();
            var memoService = new MemoService();
            Awaitable<ScanMapListResponse> mapLoad = mapService.LoadMapsAsync();
            Awaitable<MemoListResponse> memoLoad = memoService.LoadMemosAsync();

            mapList = await mapLoad;
            memoList = await memoLoad;
            hasData = true;
        }

        public static bool TryTake(out ScanMapListResponse loadedMaps, out MemoListResponse loadedMemos)
        {
            loadedMaps = mapList;
            loadedMemos = memoList;
            if (!hasData)
            {
                return false;
            }

            mapList = null;
            memoList = null;
            hasData = false;
            return true;
        }
    }
}

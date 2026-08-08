using System;
using System.Collections.Generic;
using System.Text;
using Unity.Services.Authentication;
using UnityEngine;

namespace MemoAnchor.UI
{
    public partial class MainView
    {
        private string _appliedMapSnapshot = string.Empty;
        private string _appliedMemoSnapshot = string.Empty;
        private bool _isRemoteSyncRunning;

        public async Awaitable RefreshRemoteChangesAsync()
        {
            if (!isActiveAndEnabled || !AuthenticationService.Instance.IsSignedIn)
            {
                return;
            }

            if (_isRemoteSyncRunning)
            {
                return;
            }

            _isRemoteSyncRunning = true;
            try
            {
                await RefreshMapChangesAsync();
                await RefreshMemoChangesAsync();
            }
            finally
            {
                _isRemoteSyncRunning = false;
            }
        }

        private async Awaitable RefreshMapChangesAsync()
        {
            if (_isMapListLoading)
            {
                return;
            }

            _isMapListLoading = true;
            try
            {
                ScanMapListResponse response = await _scanMapService.LoadMapsAsync();
                if (!_scanMapService.LastLoadSucceeded)
                {
                    return;
                }
                string snapshot = JsonUtility.ToJson(BuildVisibleMapResponse(response));
                if (!string.Equals(snapshot, _appliedMapSnapshot, StringComparison.Ordinal))
                {
                    ApplyMapListResponse(response);
                }
            }
            finally
            {
                _isMapListLoading = false;
            }
        }

        private async Awaitable RefreshMemoChangesAsync()
        {
            if (_isMemoListLoading)
            {
                return;
            }

            _isMemoListLoading = true;
            try
            {
                MemoListResponse response = await _memoService.LoadMemosAsync();
                if (!_memoService.LastLoadSucceeded)
                {
                    return;
                }
                string snapshot = JsonUtility.ToJson(BuildVisibleMemoResponse(response));
                if (!string.Equals(snapshot, _appliedMemoSnapshot, StringComparison.Ordinal))
                {
                    ApplyMemoListResponse(response);
                }
            }
            finally
            {
                _isMemoListLoading = false;
            }
        }

        private ScanMapListResponse BuildVisibleMapResponse(ScanMapListResponse response)
        {
            var visibleResponse = new ScanMapListResponse
            {
                createdMapId = response?.createdMapId ?? string.Empty,
                maps = response?.maps == null
                    ? new List<ScanMapItem>()
                    : new List<ScanMapItem>(response.maps)
            };

            if (_readOnlyMap != null && !visibleResponse.maps.Exists(map =>
                string.Equals(map.id, _readOnlyMap.id, StringComparison.OrdinalIgnoreCase)))
            {
                visibleResponse.maps.Add(_readOnlyMap);
            }

            return visibleResponse;
        }

        private MemoListResponse BuildVisibleMemoResponse(MemoListResponse response)
        {
            var visibleResponse = new MemoListResponse
            {
                memos = response?.memos == null
                    ? new List<MemoItem>()
                    : new List<MemoItem>(response.memos)
            };

            foreach (MemoItem memo in _readOnlyMemos)
            {
                if (!visibleResponse.memos.Exists(item =>
                    string.Equals(item.id, memo.id, StringComparison.OrdinalIgnoreCase)))
                {
                    visibleResponse.memos.Add(memo);
                }
            }

            return visibleResponse;
        }

        private void RefreshVisibleMemoDetailActions()
        {
            if (_currentMemoDetailItem != null && _memoDetailPage != null && IsVisible(_memoDetailPage))
            {
                ApplyMemoDetailWorkActions(_currentMemoDetailItem);
            }
        }

        private static string BuildMemoDetailSnapshot(MemoDetailItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(512)
                .Append(item.Id).Append('\u001f')
                .Append(item.MapId).Append('\u001f')
                .Append(item.Kind).Append('\u001f')
                .Append(item.Urgency).Append('\u001f')
                .Append(item.Place).Append('\u001f')
                .Append(item.Title).Append('\u001f')
                .Append(item.Body).Append('\u001f')
                .Append(item.AuthorPlayerId).Append('\u001f')
                .Append(item.AssigneePlayerId).Append('\u001f')
                .Append(item.WorkStatus).Append('\u001f')
                .Append(item.Location).Append('\u001f')
                .Append(item.DueText).Append('\u001f')
                .Append(item.Assignee).Append('\u001f')
                .Append(item.Author).Append('\u001f')
                .Append(item.DeletedAt).Append('\u001f')
                .Append(item.HasSpatialAnchor).Append('\u001f')
                .Append(item.ReconstructionScanId).Append('\u001f')
                .Append(item.SpatialPosition);

            foreach (MemoChecklistItem checklistItem in item.ChecklistItems)
            {
                builder.Append('\u001e').Append(checklistItem.Text).Append('\u001f').Append(checklistItem.Done);
            }
            foreach (MemoVoiceEntry voiceItem in item.VoiceItems)
            {
                builder.Append('\u001e').Append(voiceItem.name).Append('\u001f').Append(voiceItem.url);
            }
            foreach (string imageUrl in item.ImageUrls)
            {
                builder.Append('\u001e').Append(imageUrl);
            }

            return builder.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using MemoAnchor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public partial class MainTabView
    {
        private readonly ScanMapService _scanMapService = new();
        private readonly HashSet<string> _openMapAddresses = new(StringComparer.Ordinal);
        private readonly List<ScanMapItem> _scanMaps = new();
        private Button _mapListButton, _mapPreviewMenuButton;
        private VisualElement _mapPreview, _mapListOverlay, _mapListSheet, _mapListContent, _mapEmptyState;
        private Label _mapCurrentSpaceLabel, _mapCurrentAddressLabel, _mapReadModeLabel, _mapScanTimeLabel;
        private string _selectedMapId;
        private int _mapListTransitionToken;
        private bool _isMapListLoading;

        private void RegisterMapPage()
        {
            VisualElement mainRoot = _root.Q<VisualElement>("main-root");
            _mapListButton = _root.Q<Button>("map-list-button");
            _mapPreviewMenuButton = _root.Q<Button>("map-preview-menu-button");
            _mapPreview = _root.Q<VisualElement>("map-preview");
            _mapListOverlay = _root.Q<VisualElement>("map-list-overlay");
            _mapListSheet = _root.Q<VisualElement>("map-list-sheet");
            _mapListContent = _root.Q<VisualElement>("map-list-content");
            _mapCurrentSpaceLabel = _root.Q<Label>("map-current-space-label");
            _mapCurrentAddressLabel = _root.Q<Label>("map-current-address-label");
            _mapReadModeLabel = _root.Q<Label>("map-read-mode-label");
            _mapScanTimeLabel = _root.Q<Label>("map-scan-time-label");
            _mapEmptyState = _root.Q<VisualElement>("map-empty-state");

            mainRoot.Add(_mapListOverlay);
            _mapListOverlay.BringToFront();
            _mapListOverlay.AddToClassList(DIALOG_ANIM_READY_CLASS);
            _mapListOverlay.AddToClassList(HIDDEN_CLASS);
            _mapListButton.clicked += ShowMapList;
            _mapPreviewMenuButton.clicked += ShowMapList;
            _mapListOverlay.RegisterCallback<ClickEvent>(_ => HideMapList());
            _mapListSheet.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            ApplySelectedMap();
        }

        private void UnregisterMapPage()
        {
            _mapListButton.clicked -= ShowMapList;
            _mapPreviewMenuButton.clicked -= ShowMapList;
        }

        public async Awaitable RefreshMapListAsync()
        {
            if (_isMapListLoading)
            {
                return;
            }

            _isMapListLoading = true;

            try
            {
                ScanMapListResponse response = await _scanMapService.LoadMapsAsync();
                _scanMaps.Clear();
                _scanMaps.AddRange(response.maps);

                if (_scanMaps.Count == 0)
                {
                    _selectedMapId = string.Empty;
                    _openMapAddresses.Clear();
                }
                else if (string.IsNullOrWhiteSpace(_selectedMapId) || !_scanMaps.Exists(map => map.id == _selectedMapId))
                {
                    _selectedMapId = _scanMaps[0].id;
                    _openMapAddresses.Add(GetMapAddressKey(_scanMaps[0]));
                }

                RebuildMapList();
                ApplySelectedMap();
            }
            finally
            {
                _isMapListLoading = false;
            }
        }

        private void ShowMapList()
        {
            RebuildMapList();
            _mapListTransitionToken++;
            _mapListOverlay.RemoveFromClassList(HIDDEN_CLASS);
            _mapListOverlay.RemoveFromClassList(DIALOG_OPEN_CLASS);

            int token = _mapListTransitionToken;
            _mapListOverlay.schedule.Execute(() =>
            {
                if (token != _mapListTransitionToken)
                {
                    return;
                }

                _mapListOverlay.AddToClassList(DIALOG_OPEN_CLASS);
            }).ExecuteLater(16);
        }

        private void HideMapList()
        {
            _mapListTransitionToken++;
            int token = _mapListTransitionToken;

            _mapListOverlay.RemoveFromClassList(DIALOG_OPEN_CLASS);
            _mapListOverlay.schedule.Execute(() =>
            {
                if (token != _mapListTransitionToken)
                {
                    return;
                }

                _mapListOverlay.AddToClassList(HIDDEN_CLASS);
            }).ExecuteLater(240);
        }

        private void RebuildMapList()
        {
            _mapListContent.Clear();

            Dictionary<string, List<ScanMapItem>> mapsByAddress = new(StringComparer.Ordinal);
            foreach (ScanMapItem map in _scanMaps)
            {
                string address = GetMapAddressKey(map);
                if (!mapsByAddress.TryGetValue(address, out List<ScanMapItem> maps))
                {
                    maps = new List<ScanMapItem>();
                    mapsByAddress.Add(address, maps);
                }

                maps.Add(map);
            }

            foreach (KeyValuePair<string, List<ScanMapItem>> pair in mapsByAddress)
            {
                AddMapAddressRow(pair.Key, pair.Value);
            }
        }

        private void AddMapAddressRow(string address, List<ScanMapItem> maps)
        {
            bool isOpen = _openMapAddresses.Contains(address);
            Button addressRow = new();
            addressRow.AddToClassList("scan-address-list-item");
            addressRow.AddToClassList("scan-address-building-item");
            addressRow.AddToClassList("map-list-address-row");
            addressRow.clicked += () =>
            {
                if (!_openMapAddresses.Remove(address))
                {
                    _openMapAddresses.Add(address);
                }

                RebuildMapList();
            };

            Label addressLabel = new(address);
            addressLabel.AddToClassList("scan-address-list-text");
            addressLabel.AddToClassList("map-list-address-text");

            VisualElement chevron = new();
            chevron.AddToClassList("map-list-chevron");
            chevron.EnableInClassList("is-open", isOpen);

            addressRow.Add(addressLabel);
            addressRow.Add(chevron);
            _mapListContent.Add(addressRow);

            if (!isOpen)
            {
                return;
            }

            foreach (ScanMapItem map in maps)
            {
                AddMapSpaceRow(map);
            }
        }

        private void AddMapSpaceRow(ScanMapItem map)
        {
            Button spaceRow = new();
            spaceRow.AddToClassList("scan-address-list-item");
            spaceRow.AddToClassList("map-list-space-row");
            spaceRow.EnableInClassList(SELECTED_CLASS, map.id == _selectedMapId);
            spaceRow.clicked += () =>
            {
                _selectedMapId = map.id;
                _openMapAddresses.Add(GetMapAddressKey(map));
                ApplySelectedMap();
                RebuildMapList();
                HideMapList();
            };

            Label spaceLabel = new(map.spaceName);
            spaceLabel.AddToClassList("scan-address-list-text");
            spaceLabel.AddToClassList("map-list-space-text");

            spaceRow.Add(spaceLabel);
            _mapListContent.Add(spaceRow);
        }

        private void ApplySelectedMap()
        {
            ScanMapItem selectedMap = _scanMaps.Find(map => map.id == _selectedMapId);
            bool hasMap = selectedMap != null;
            _mapPreview.EnableInClassList("is-empty", !hasMap);
            SetVisible(_mapReadModeLabel, hasMap);
            SetVisible(_mapScanTimeLabel, hasMap);
            SetVisible(_mapEmptyState, !hasMap);

            if (!hasMap)
            {
                _mapCurrentSpaceLabel.text = "3D MAP";
                _mapCurrentAddressLabel.text = string.Empty;
                _mapScanTimeLabel.text = string.Empty;
                return;
            }

            _mapCurrentSpaceLabel.text = selectedMap.spaceName;
            _mapCurrentAddressLabel.text = GetMapAddressKey(selectedMap);
            _mapScanTimeLabel.text = $"스캔일시 : {FormatScanTime(selectedMap.scanCreatedAt)}";
        }

        private static string GetMapAddressKey(ScanMapItem map)
        {
            return string.IsNullOrWhiteSpace(map.roadAddress) ? map.address : map.roadAddress;
        }

        private static string FormatScanTime(string scanCreatedAt)
        {
            if (!DateTimeOffset.TryParse(scanCreatedAt, out DateTimeOffset parsed))
            {
                return string.Empty;
            }

            return parsed.LocalDateTime.ToString("yyyy/MM/dd h:mm tt", CultureInfo.InvariantCulture);
        }
    }
}

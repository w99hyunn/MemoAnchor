using System;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public partial class MainView
    {
        private VisualElement _homeMapCardContainer;
        private Label _homeMapEmptyLabel;

        private void RegisterHomeMapCards()
        {
            _homeMapCardContainer = _root.Q<VisualElement>("home-map-card-container");
            _homeMapEmptyLabel = _root.Q<Label>("home-map-empty-label");
            RebuildHomeMapCards();
        }

        private void RebuildHomeMapCards()
        {
            if (_homeMapCardContainer == null)
            {
                return;
            }

            _homeMapCardContainer.Clear();
            int visibleMapCount = 0;
            foreach (ScanMapItem map in _scanMaps)
            {
                if (!IsParticipatingMap(map))
                {
                    continue;
                }

                _homeMapCardContainer.Add(CreateHomeMapCard(map));
                visibleMapCount++;
            }

            SetVisible(_homeMapEmptyLabel, visibleMapCount == 0);
        }

        private VisualElement CreateHomeMapCard(ScanMapItem map)
        {
            VisualElement card = new();
            card.AddToClassList("scan-map-card");
            VisualElement thumbnail = new();
            thumbnail.AddToClassList("scan-map-thumbnail");
            VisualElement info = new();
            info.AddToClassList("scan-map-info");
            Label title = new(map.spaceName);
            title.AddToClassList("scan-map-title");
            Label address = new(GetFirstNonEmpty(map.roadAddress, map.address, string.Empty));
            address.AddToClassList("scan-map-subtitle");

            info.Add(title);
            info.Add(address);
            card.Add(thumbnail);
            card.Add(info);
            card.RegisterCallback<ClickEvent>(_ =>
            {
                _selectedMapId = map.id;
                ApplySelectedMap();
                RequestTabSwitch(3);
            });
            return card;
        }

        private static bool IsParticipatingMap(ScanMapItem map)
        {
            return string.Equals(map.currentUserRole, "manager", StringComparison.OrdinalIgnoreCase)
                || string.Equals(map.currentUserRole, "repairer", StringComparison.OrdinalIgnoreCase);
        }
    }
}

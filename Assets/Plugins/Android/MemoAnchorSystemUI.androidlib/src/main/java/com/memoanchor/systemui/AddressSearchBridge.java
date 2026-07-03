package com.memoanchor.systemui;

import android.app.Activity;

public final class AddressSearchBridge {
    private AddressSearchBridge() {
    }

    public static void open(Activity activity, String unityGameObjectName) {
        KakaoPostcodeOverlay.show(activity, unityGameObjectName);
    }
}

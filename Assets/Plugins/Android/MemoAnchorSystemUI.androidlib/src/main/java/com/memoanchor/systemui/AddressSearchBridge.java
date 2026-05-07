package com.memoanchor.systemui;

import android.app.Activity;
import android.content.Intent;

public final class AddressSearchBridge {
    private AddressSearchBridge() {
    }

    public static void open(Activity activity, String unityGameObjectName) {
        Intent intent = new Intent(activity, KakaoPostcodeActivity.class);
        intent.putExtra(KakaoPostcodeActivity.EXTRA_UNITY_GAME_OBJECT, unityGameObjectName);
        activity.startActivity(intent);
    }
}

package com.memoanchor.systemui;

import android.app.Activity;
import android.content.Intent;

public final class ShareBridge {
    private ShareBridge() {
    }

    public static void shareText(Activity activity, String subject, String text) {
        Intent sendIntent = new Intent(Intent.ACTION_SEND);
        sendIntent.setType("text/plain");
        sendIntent.putExtra(Intent.EXTRA_SUBJECT, subject);
        sendIntent.putExtra(Intent.EXTRA_TEXT, text);
        Intent chooserIntent = Intent.createChooser(sendIntent, "메모 공유");
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                activity.startActivity(chooserIntent);
            }
        });
    }
}

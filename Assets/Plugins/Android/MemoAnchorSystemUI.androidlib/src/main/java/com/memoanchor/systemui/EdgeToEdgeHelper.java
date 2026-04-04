package com.memoanchor.systemui;

import android.os.Build;
import android.view.View;
import android.view.Window;
import android.view.WindowInsets;
import android.view.WindowManager;

public final class EdgeToEdgeHelper {
    private EdgeToEdgeHelper() {
    }

    public static void apply(Window window) {
        int sdk = Build.VERSION.SDK_INT;

        window.clearFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN);
        window.addFlags(WindowManager.LayoutParams.FLAG_DRAWS_SYSTEM_BAR_BACKGROUNDS);
        window.setStatusBarColor(0);
        window.setNavigationBarColor(0);

        if (sdk >= Build.VERSION_CODES.Q) {
            window.setStatusBarContrastEnforced(false);
            window.setNavigationBarContrastEnforced(false);
        }

        View decorView = window.getDecorView();
        int visibility = decorView.getSystemUiVisibility();
        visibility &= ~View.SYSTEM_UI_FLAG_FULLSCREEN;
        visibility &= ~View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY;

        if (sdk >= Build.VERSION_CODES.R) {
            window.setDecorFitsSystemWindows(false);
            window.getInsetsController().show(
                    WindowInsets.Type.statusBars() | WindowInsets.Type.navigationBars());
            decorView.setSystemUiVisibility(visibility);
        } else {
            visibility |= View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                    | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                    | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION;
            decorView.setSystemUiVisibility(visibility);
            window.addFlags(WindowManager.LayoutParams.FLAG_FORCE_NOT_FULLSCREEN);
        }
    }
}

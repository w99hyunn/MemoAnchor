package com.memoanchor.systemui;

import android.os.Build;
import android.view.View;
import android.view.Window;
import android.view.WindowInsets;
import android.view.WindowInsetsController;
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
            WindowInsetsController controller = window.getInsetsController();
            controller.show(
                    WindowInsets.Type.statusBars() | WindowInsets.Type.navigationBars());
            // 밝은 배경용: 상태바 시계·아이콘을 어둡게
            controller.setSystemBarsAppearance(
                    WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS,
                    WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS);
            decorView.setSystemUiVisibility(visibility);
        } else {
            visibility |= View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                    | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                    | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION;
            if (sdk >= Build.VERSION_CODES.M) {
                visibility |= View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR;
            }
            decorView.setSystemUiVisibility(visibility);
            window.addFlags(WindowManager.LayoutParams.FLAG_FORCE_NOT_FULLSCREEN);
        }
    }
}

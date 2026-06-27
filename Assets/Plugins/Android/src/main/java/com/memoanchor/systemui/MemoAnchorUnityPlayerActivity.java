package com.memoanchor.systemui;

import android.graphics.Color;
import android.os.Build;
import android.os.Bundle;
import android.view.View;
import android.view.Window;
import android.view.WindowInsets;
import android.view.WindowInsetsController;
import android.view.WindowManager;

import com.unity3d.player.UnityPlayerActivity;

public class MemoAnchorUnityPlayerActivity extends UnityPlayerActivity {
    private boolean isApplyingSystemBars;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        installSystemBarsListener(getWindow().getDecorView());
        installSystemBarsListener(mUnityPlayer.getFrameLayout());
        applySystemBars();
    }

    @Override
    protected void onResume() {
        super.onResume();
        applySystemBars();
    }

    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus) {
            applySystemBars();
        }
    }

    private void applySystemBars() {
        if (isApplyingSystemBars) {
            return;
        }

        isApplyingSystemBars = true;
        Window window = getWindow();
        int sdk = Build.VERSION.SDK_INT;

        window.clearFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN);
        window.clearFlags(WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS);
        window.addFlags(WindowManager.LayoutParams.FLAG_FORCE_NOT_FULLSCREEN);
        window.addFlags(WindowManager.LayoutParams.FLAG_DRAWS_SYSTEM_BAR_BACKGROUNDS);
        window.setStatusBarColor(Color.TRANSPARENT);
        window.setNavigationBarColor(Color.WHITE);

        if (sdk >= Build.VERSION_CODES.Q) {
            window.setStatusBarContrastEnforced(false);
            window.setNavigationBarContrastEnforced(true);
        }

        applySystemUiVisibility(window.getDecorView());
        applySystemUiVisibility(mUnityPlayer.getFrameLayout());

        isApplyingSystemBars = false;
    }

    private void applySystemUiVisibility(View view) {
        int visibility = view.getSystemUiVisibility();
        visibility &= ~View.SYSTEM_UI_FLAG_FULLSCREEN;
        visibility &= ~View.SYSTEM_UI_FLAG_HIDE_NAVIGATION;
        visibility &= ~View.SYSTEM_UI_FLAG_IMMERSIVE;
        visibility &= ~View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY;
        visibility &= ~View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION;
        visibility |= View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                | View.SYSTEM_UI_FLAG_LAYOUT_STABLE;

        int sdk = Build.VERSION.SDK_INT;
        if (sdk >= Build.VERSION_CODES.R) {
            getWindow().setDecorFitsSystemWindows(false);
            WindowInsetsController controller = getWindow().getInsetsController();
            controller.show(WindowInsets.Type.statusBars() | WindowInsets.Type.navigationBars());
            controller.setSystemBarsAppearance(
                    WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS
                            | WindowInsetsController.APPEARANCE_LIGHT_NAVIGATION_BARS,
                    WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS
                            | WindowInsetsController.APPEARANCE_LIGHT_NAVIGATION_BARS);
        } else {
            if (sdk >= Build.VERSION_CODES.M) {
                visibility |= View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR;
            }
            if (sdk >= Build.VERSION_CODES.O) {
                visibility |= View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR;
            }
        }

        view.setSystemUiVisibility(visibility);
    }

    private void installSystemBarsListener(View view) {
        view.setOnSystemUiVisibilityChangeListener(visibility -> {
            if (isApplyingSystemBars) {
                return;
            }

            boolean hidesSystemBars = (visibility & View.SYSTEM_UI_FLAG_FULLSCREEN) != 0
                    || (visibility & View.SYSTEM_UI_FLAG_HIDE_NAVIGATION) != 0
                    || (visibility & View.SYSTEM_UI_FLAG_IMMERSIVE) != 0
                    || (visibility & View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY) != 0;

            if (hidesSystemBars) {
                applySystemBars();
            }
        });
    }
}

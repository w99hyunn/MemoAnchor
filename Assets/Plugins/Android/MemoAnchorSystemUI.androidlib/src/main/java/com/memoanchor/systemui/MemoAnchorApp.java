package com.memoanchor.systemui;

import android.app.Activity;
import android.app.Application;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.View;
import android.view.ViewTreeObserver;

/**
 * Unity가 레이아웃·resume 이후 풀스크린 플래그를 다시 거는 경우에 대비해,
 * 한 경로({@link #stagger})로만 타이밍을 나눠 적용한다.
 */
public class MemoAnchorApp extends Application {

    private final Handler mainHandler = new Handler(Looper.getMainLooper());

    private final ActivityLifecycleCallbacks callbacks = new ActivityLifecycleCallbacks() {
        @Override
        public void onActivityCreated(Activity activity, Bundle savedInstanceState) {
            if (!isUnityActivity(activity)) {
                return;
            }
            hookFirstPreDraw(activity);
            stagger(activity, true, 100, 320, 600);
        }

        @Override
        public void onActivityResumed(Activity activity) {
            if (!isUnityActivity(activity)) {
                return;
            }
            stagger(activity, true, 120, 360, 720);
        }

        @Override
        public void onActivityPostResumed(Activity activity) {
            if (!isUnityActivity(activity)) {
                return;
            }
            stagger(activity, false, 200, 480);
        }

        @Override
        public void onActivityStarted(Activity activity) {
        }

        @Override
        public void onActivityPaused(Activity activity) {
        }

        @Override
        public void onActivityStopped(Activity activity) {
        }

        @Override
        public void onActivitySaveInstanceState(Activity activity, Bundle outState) {
        }

        @Override
        public void onActivityDestroyed(Activity activity) {
        }
    };

    /** 첫 그리기 직전에 한 번 (콜드 스타트). */
    private void hookFirstPreDraw(final Activity activity) {
        final View decor = activity.getWindow().getDecorView();
        decor.getViewTreeObserver().addOnPreDrawListener(new ViewTreeObserver.OnPreDrawListener() {
            @Override
            public boolean onPreDraw() {
                applySafe(activity);
                decor.getViewTreeObserver().removeOnPreDrawListener(this);
                return true;
            }
        });
    }

    /**
     * 즉시 적용 → (옵션) 다음 프레임 → 지연들.
     * 포커스 리스너는 resume/postResume과 겹쳐 제거함.
     */
    private void stagger(final Activity activity, boolean postNextFrame, int... delaysMs) {
        applySafe(activity);
        if (postNextFrame) {
            final View decor = activity.getWindow().getDecorView();
            decor.post(new Runnable() {
                @Override
                public void run() {
                    applySafe(activity);
                }
            });
        }
        for (final int ms : delaysMs) {
            mainHandler.postDelayed(new Runnable() {
                @Override
                public void run() {
                    applySafe(activity);
                }
            }, ms);
        }
    }

    private static void applySafe(Activity activity) {
        if (activity.isDestroyed()) {
            return;
        }
        EdgeToEdgeHelper.apply(activity.getWindow());
    }

    private static boolean isUnityActivity(Activity activity) {
        return activity.getClass().getName().contains("UnityPlayer");
    }

    @Override
    public void onCreate() {
        super.onCreate();
        registerActivityLifecycleCallbacks(callbacks);
    }
}

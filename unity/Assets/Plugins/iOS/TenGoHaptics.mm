// Retours haptiques et accessibilité pour tenGO (Unity → iOS).
// Mêmes générateurs que HapticManager.swift : un UIImpactFeedbackGenerator par
// style (light, medium, heavy, soft, rigid) et un UINotificationFeedbackGenerator.
// Appelé sur le fil principal (thread Unity = fil principal sur iOS).
// Styles (HapticStyle.cs) : 0 light, 1 medium, 2 heavy, 3 soft, 4 rigid.
// Notifications (HapticNotification.cs) : 0 success, 1 warning, 2 error.

#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *TenGoImpactGenerators[5];
static UINotificationFeedbackGenerator *TenGoNotificationGenerator;

static void TenGoEnsureGenerators(void)
{
    if (TenGoNotificationGenerator != nil) return;
    UIImpactFeedbackStyle styles[5] = {
        UIImpactFeedbackStyleLight, UIImpactFeedbackStyleMedium, UIImpactFeedbackStyleHeavy,
        UIImpactFeedbackStyleSoft, UIImpactFeedbackStyleRigid,
    };
    for (int i = 0; i < 5; i++)
        TenGoImpactGenerators[i] = [[UIImpactFeedbackGenerator alloc] initWithStyle:styles[i]];
    TenGoNotificationGenerator = [[UINotificationFeedbackGenerator alloc] init];
}

extern "C" {

void _TenGoHapticPrepare(void)
{
    TenGoEnsureGenerators();
    for (int i = 0; i < 5; i++) [TenGoImpactGenerators[i] prepare];
    [TenGoNotificationGenerator prepare];
}

void _TenGoHapticImpact(int style, float intensity)
{
    TenGoEnsureGenerators();
    if (style < 0 || style > 4) style = 0;
    CGFloat value = intensity < 0 ? 0 : (intensity > 1 ? 1 : intensity);
    [TenGoImpactGenerators[style] impactOccurredWithIntensity:value];
}

void _TenGoHapticNotify(int type)
{
    TenGoEnsureGenerators();
    UINotificationFeedbackType t = type == 1 ? UINotificationFeedbackTypeWarning
        : (type == 2 ? UINotificationFeedbackTypeError : UINotificationFeedbackTypeSuccess);
    [TenGoNotificationGenerator notificationOccurred:t];
}

bool _TenGoReduceMotionEnabled(void)
{
    return UIAccessibilityIsReduceMotionEnabled();
}

}

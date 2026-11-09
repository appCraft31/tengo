// Reprise des données de l'app iOS native tenGO 3.3.0 (étape 3 du portage Unity).
//
// Lit, sans jamais les modifier, les clés du jeu dans
// [NSUserDefaults standardUserDefaults] et les renvoie en JSON typé :
//
//   { "<clé>": { "t": "<type>", "v": <valeur> }, … }
//
// Types : bool, int, double, string, date (secondes depuis le 1er janvier
// 2001, timeIntervalSinceReferenceDate), data (NSData UTF-8, en texte : les
// JSON de JSONEncoder), bytes (NSData binaire, base64), array (NSArray de
// nombres ou de chaînes), unsupported (type laissé de côté).
//
// Demande (JSON) : { "keys": [clés exactes], "prefixes": [préfixes] }.
// Décodage côté C# : TenGo.Core.IosLegacyImporter.ParsePluginJson.
//
// Écrit pour fonctionner avec ou sans ARC.

#import <Foundation/Foundation.h>
#include <math.h>
#include <string.h>

static NSDictionary *TenGoLegacyEncodeNumber(NSNumber *number)
{
    // NSNumber booléen : la classe concrète est __NSCFBoolean (kCFBooleanTrue/False).
    if (CFGetTypeID((CFTypeRef)number) == CFBooleanGetTypeID())
        return @{ @"t": @"bool", @"v": @([number boolValue]) };
    const char *type = [number objCType];
    if (strcmp(type, @encode(double)) == 0 || strcmp(type, @encode(float)) == 0)
    {
        double d = [number doubleValue];
        if (isnan(d) || isinf(d)) return @{ @"t": @"unsupported", @"v": @"non-finite" };
        return @{ @"t": @"double", @"v": @(d) };
    }
    return @{ @"t": @"int", @"v": @([number longLongValue]) };
}

static id TenGoLegacyArrayItem(id item)
{
    if ([item isKindOfClass:[NSString class]]) return item;
    if ([item isKindOfClass:[NSNumber class]])
    {
        NSNumber *n = (NSNumber *)item;
        if (CFGetTypeID((CFTypeRef)n) == CFBooleanGetTypeID()) return @([n boolValue] ? 1 : 0);
        const char *type = [n objCType];
        if (strcmp(type, @encode(double)) == 0 || strcmp(type, @encode(float)) == 0)
        {
            double d = [n doubleValue];
            return (isnan(d) || isinf(d)) ? nil : @(d);
        }
        return @([n longLongValue]);
    }
    return nil;
}

static NSDictionary *TenGoLegacyEncode(id value)
{
    if ([value isKindOfClass:[NSString class]])
        return @{ @"t": @"string", @"v": value };
    if ([value isKindOfClass:[NSNumber class]])
        return TenGoLegacyEncodeNumber((NSNumber *)value);
    if ([value isKindOfClass:[NSDate class]])
        return @{ @"t": @"date", @"v": @([(NSDate *)value timeIntervalSinceReferenceDate]) };
    if ([value isKindOfClass:[NSData class]])
    {
        NSData *data = (NSData *)value;
        NSString *text = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
#if !__has_feature(objc_arc)
        [text autorelease];
#endif
        if (text != nil) return @{ @"t": @"data", @"v": text };
        return @{ @"t": @"bytes", @"v": [data base64EncodedStringWithOptions:0] };
    }
    if ([value isKindOfClass:[NSArray class]])
    {
        NSMutableArray *items = [NSMutableArray array];
        for (id item in (NSArray *)value)
        {
            id encoded = TenGoLegacyArrayItem(item);
            if (encoded == nil) return @{ @"t": @"unsupported", @"v": @"array" };
            [items addObject:encoded];
        }
        return @{ @"t": @"array", @"v": items };
    }
    return @{ @"t": @"unsupported", @"v": NSStringFromClass([value class]) ?: @"?" };
}

static char *TenGoLegacyCopy(NSString *text)
{
    const char *utf8 = [text UTF8String];
    return strdup(utf8 != NULL ? utf8 : "{}");
}

extern "C" char *TenGoLegacy_ReadDefaults(const char *requestJson)
{
    @autoreleasepool
    {
        NSArray *keys = @[];
        NSArray *prefixes = @[];
        if (requestJson != NULL)
        {
            NSData *requestData = [NSData dataWithBytes:requestJson length:strlen(requestJson)];
            id request = [NSJSONSerialization JSONObjectWithData:requestData options:0 error:nil];
            if ([request isKindOfClass:[NSDictionary class]])
            {
                id k = request[@"keys"];
                id p = request[@"prefixes"];
                if ([k isKindOfClass:[NSArray class]]) keys = k;
                if ([p isKindOfClass:[NSArray class]]) prefixes = p;
            }
        }

        // Domaine persistant de l'app uniquement : ni le domaine global
        // (NSGlobalDomain, langue…), ni les valeurs enregistrées par registerDefaults.
        NSString *bundleId = [[NSBundle mainBundle] bundleIdentifier];
        NSDictionary *domain = bundleId != nil ? [[NSUserDefaults standardUserDefaults] persistentDomainForName:bundleId] : nil;
        if (domain == nil) domain = @{};

        NSMutableDictionary *result = [NSMutableDictionary dictionary];
        for (id key in keys)
        {
            if (![key isKindOfClass:[NSString class]]) continue;
            id value = domain[key];
            if (value != nil) result[key] = TenGoLegacyEncode(value);
        }
        for (NSString *key in domain)
        {
            if (result[key] != nil) continue;
            for (id prefix in prefixes)
            {
                if ([prefix isKindOfClass:[NSString class]] && [key hasPrefix:prefix])
                {
                    result[key] = TenGoLegacyEncode(domain[key]);
                    break;
                }
            }
        }

        NSError *error = nil;
        NSData *json = [NSJSONSerialization dataWithJSONObject:result options:0 error:&error];
        // JSON volontairement invalide : côté C#, la lecture échoue et sera
        // retentée au prochain lancement, au lieu de conclure « aucune donnée ».
        if (json == nil) return TenGoLegacyCopy(@"!serialisation");
        NSString *text = [[NSString alloc] initWithData:json encoding:NSUTF8StringEncoding];
        char *copy = TenGoLegacyCopy(text);
#if !__has_feature(objc_arc)
        [text release];
#endif
        return copy;
    }
}

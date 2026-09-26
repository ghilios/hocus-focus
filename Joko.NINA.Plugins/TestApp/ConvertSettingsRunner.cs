#region "copyright"

/*
    Copyright © 2021 - 2026 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Enum;
using NINA.Joko.Plugins.HocusFocus.StarDetection;
using NINA.Profile;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Logger = NINA.Core.Utility.Logger;

namespace TestApp {

    /// <summary>
    /// Converts a plugin settings EXPORT (the <c>HocusFocusStarDetectionSettings</c> file the Star Detection
    /// options page and the per-filter store write) into the flat option bag every harness runner's
    /// <c>--settings</c> expects.
    ///
    /// <para><b>Why this exists.</b> The two files look interchangeable and are not: the harness file is a
    /// <c>{ Options: { key: value } }</c> snapshot of plugin option keys, while an export nests typed values
    /// under <c>starDetection</c>. Newtonsoft deserializes an export into the harness shape without complaint
    /// and yields an EMPTY option bag, so the run silently uses stock defaults — the settings appear to have been
    /// honoured and were not. The conversion goes through the plugin's own
    /// <c>StarDetectionOptions.ApplyImportedSnapshot</c>, the same path the Import button uses, so the key
    /// mapping cannot drift from what the options class actually reads.</para>
    /// </summary>
    internal static class ConvertSettingsRunner {

        public static async Task Run(string[] args) {
            try {
                await Task.Run(() => RunImpl(args));
            } catch (Exception ex) {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                Console.Error.WriteLine(ex.ToString());
                Logger.Error(ex, "convert-settings failed");
                Environment.ExitCode = 1;
            }
        }

        private static void RunImpl(string[] args) {
            var importPath = DiagnosticUtil.GetArg(args, "--import");
            var outPath = DiagnosticUtil.GetArg(args, "--out");
            if (string.IsNullOrWhiteSpace(importPath) || string.IsNullOrWhiteSpace(outPath)) {
                Console.Error.WriteLine("Usage: TestApp convert-settings --import <export.json> --out <harness-settings.json> [--set Key=Value ...]");
                Environment.ExitCode = 2;
                return;
            }
            if (!File.Exists(importPath)) {
                throw new FileNotFoundException($"Export not found: {importPath}", importPath);
            }

            Logger.SetLogLevel(LogLevelEnum.ERROR);
            if (Application.Current == null) {
                new Application();
            }

            var export = JsonConvert.DeserializeObject<StarDetectionSettingsExport>(File.ReadAllText(importPath));
            if (export?.StarDetection == null) {
                throw new InvalidOperationException($"{importPath} carries no starDetection block -- it is not a plugin settings export.");
            }
            Console.WriteLine($"Import: {importPath} (fileType={export.FileType}, schema={export.SchemaVersion}, filter={export.FilterName ?? "-"}, plugin {export.PluginVersion})");

            var profileService = new ProfileService();
            profileService.TryLoad(string.Empty);

            var bag = new Dictionary<string, string>(StringComparer.Ordinal);
            var options = new StarDetectionOptions(profileService, new HarnessSettingsStore.FileOptionsAccessor(bag));
            options.ApplyImportedSnapshot(export.StarDetection);

            // --set overrides, applied to the produced bag so an arm can pin one knob without hand-editing.
            foreach (var kv in OverridePairs(args)) {
                bag[kv.Key] = kv.Value;
                Console.WriteLine($"  override {kv.Key}={kv.Value}");
            }

            var file = new HarnessSettingsStore.HarnessSettingsFile {
                ExportedFromProfile = $"converted from {Path.GetFileName(importPath)}",
                ExportedAtUtc = DateTime.UtcNow,
                Options = bag
            };
            var dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir)) {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(outPath, JsonConvert.SerializeObject(file, Formatting.Indented));
            Console.WriteLine($"Wrote {outPath} ({bag.Count} option keys)");
            foreach (var key in new[] { "UseAdvanced", "PSFResolution", "UsePSFAbsoluteDeviation", "HotpixelFiltering", "HotpixelThresholdingEnabled", "NoiseClippingMultiplier", "BrightnessSensitivity", "StructureLayers", "MinHFR" }) {
                Console.WriteLine($"  {key} = {(bag.TryGetValue(key, out var v) ? v : "<absent>")}");
            }
        }

        private static IEnumerable<KeyValuePair<string, string>> OverridePairs(string[] args) {
            for (var i = 0; args != null && i < args.Length - 1; ++i) {
                if (!string.Equals(args[i], "--set", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                var raw = args[i + 1];
                var eq = raw.IndexOf('=');
                if (eq <= 0) {
                    throw new ArgumentException($"--set expects Key=Value, got '{raw}'");
                }
                yield return new KeyValuePair<string, string>(raw.Substring(0, eq), raw.Substring(eq + 1));
            }
        }
    }
}

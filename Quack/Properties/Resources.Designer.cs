﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Quack.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Quack.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // Requires &quot;DeterministicPose&quot; and &quot;ModSettingCommands&quot; plugins to be installed
        ///
        ///// Recommended to use ModAutoTagger plugin to define the mod bulk tags for the conflict resolution
        ///// Chat filters plugin like for example NoSoliciting can help reduce noise when running commands
        ///
        ///// Collection name
        ///var ARGS = &apos;Self&apos;;
        ///
        ///const IDLE_PSEUDO_EMOTE = {
        ///    command: &apos;/idle&apos;,
        ///    actionTimelineKeys: [],
        ///    poseKeys: [&apos;emote/pose00_loop&apos;, &apos;emote/pose01_loop&apos;, &apos;emote/pose02_loop&apos;, &apos;emote/pose03_loop&apos;, &apos;emote [rest of string was truncated]&quot;;.
        /// </summary>
        public static string CustomEmotesJsContent {
            get {
                return ResourceManager.GetString("CustomEmotesJsContent", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // Target name
        ///var ARGS = &apos;self&apos;;
        ///
        ///function main(profilesJson) {
        ///    const profiles = JSON.parse(profilesJson);
        ///    const macros = profiles.flatMap(p =&gt; {
        ///        return [{
        ///            name: `Enable Profile [${p.Item2}]`,
        ///            path: `Customizations/${p.Item2}/Enable`,
        ///            tags: [&apos;customize&apos;, &apos;profile&apos;, &apos;enable&apos;],
        ///            args: ARGS,
        ///            content: `/customize profile enable {0},${p.Item2}`
        ///        },{
        ///            name: `Disable Profile [${p.Item2}]`,
        ///            path:  [rest of string was truncated]&quot;;.
        /// </summary>
        public static string CustomizeProfilesJsContent {
            get {
                return ResourceManager.GetString("CustomizeProfilesJsContent", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to function main(emotesJson) {
        ///    const emotes = JSON.parse(emotesJson);
        ///    const macros = emotes.flatMap(e =&gt; {
        ///        var categoryName = `${e.category[0].toUpperCase()}${e.category.slice(1)}`;
        ///        return [{
        ///            name: `Emote [${e.name}]`,
        ///            path: `Emotes/${categoryName}/${e.name}/Execute`,
        ///            tags: [&apos;emote&apos;, e.category.toLowerCase(), e.command],
        ///            content: e.command
        ///        }, {
        ///            name: `Emote [${e.name}] [Motion]`,
        ///            path: `Emotes/${c [rest of string was truncated]&quot;;.
        /// </summary>
        public static string EmotesJsContent {
            get {
                return ResourceManager.GetString("EmotesJsContent", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // Recommended to use ModAutoTagger plugin to define the mod bulk tags for the conflict resolution
        ///// Chat filters plugin like for example NoSoliciting can help reduce noise when running commands
        ///
        ///var ARGS = &apos;Self&apos;;
        ///
        ///function main(designsJson) {
        ///    const designs = JSON.parse(designsJson);
        ///    const macros = designs.map(d =&gt; {
        ///        const contentLines = [
        ///            &apos;/penumbra bulktag disable {0} | all&apos;,
        ///            `/glamour apply ${d.id} | &lt;me&gt;; true`
        ///        ];
        ///
        ///        return {
        ///         [rest of string was truncated]&quot;;.
        /// </summary>
        public static string GlamoursJsContent {
            get {
                return ResourceManager.GetString("GlamoursJsContent", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // Second IPC argument value (WorldId) can be found as key in %appdata%\xivlauncher\pluginConfigs\Honorific.json
        ///
        ///function main(titlesJson) {
        ///    const titles = JSON.parse(titlesJson);
        ///
        ///    const disableHonorificsMacro = {
        ///        name: `Disable Honorifics`,
        ///        path: &apos;Macros/Customs/Disable Honorifics&apos;,
        ///        tags: [&apos;honorifics&apos;, &apos;titles&apos;, &apos;disable&apos;],
        ///        command: &apos;/disablehonorifics&apos;,
        ///        content: titles.map(t =&gt; `/honorific title disable ${t.Title}`).join(&quot;\n&quot;)
        ///    };
        ///
        ///    cons [rest of string was truncated]&quot;;.
        /// </summary>
        public static string HonorificsJsContent {
            get {
                return ResourceManager.GetString("HonorificsJsContent", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // Requires Simple Tweak &gt; Command &gt; Equip Job Command to be enabled
        ///
        ///const JOBS = [
        ///    &apos;ARC&apos;, &apos;ACN&apos;, &apos;CNJ&apos;, &apos;GLA&apos;, &apos;LNC&apos;, &apos;MRD&apos;, &apos;PGL&apos;, &apos;ROG&apos;, &apos;THM&apos;,
        ///    &apos;ALC&apos;, &apos;ARM&apos;, &apos;BSM&apos;, &apos;CUL&apos;, &apos;CRP&apos;, &apos;GSM&apos;, &apos;LTW&apos;, &apos;WVR&apos;,
        ///    &apos;BTN&apos;, &apos;FSH&apos;, &apos;MIN&apos;,
        ///    &apos;BLM&apos;, &apos;BRD&apos;, &apos;DRG&apos;, &apos;MNK&apos;, &apos;NIN&apos;, &apos;PLD&apos;, &apos;SCH&apos;, &apos;SMN&apos;, &apos;WAR&apos;, &apos;WHM&apos;, &apos;SAM&apos;, &apos;RDM&apos;, &apos;MCH&apos;, &apos;DRK&apos;, &apos;AST&apos;, &apos;GNB&apos;, &apos;DNC&apos;, &apos;SGE&apos;, &apos;RPR&apos;, &apos;VPR&apos;, &apos;PTN&apos;, &apos;BLU&apos;
        ///];
        ///
        ///function main() {
        ///    const macros = JOBS.map(j =&gt; {
        ///        return {
        ///            name: `Equip Job [${ [rest of string was truncated]&quot;;.
        /// </summary>
        public static string JobsJsContent {
            get {
                return ResourceManager.GetString("JobsJsContent", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to function main(rawMacrosJson, localPlayerInfoJson) {
        ///    const rawMacros = JSON.parse(rawMacrosJson);
        ///    const localPlayerInfo = JSON.parse(localPlayerInfoJson);
        ///
        ///    const macros = rawMacros.flatMap(m =&gt; {
        ///        const name = `Macro [${m.name || &apos;Blank&apos;}]`;
        ///        if (m.set == 0) {
        ///            return [{
        ///                name: name,
        ///                tags: [&apos;individual&apos;, &apos;macro&apos;, `${m.index}`],
        ///                path: `Macros/Individual/${localPlayerInfo.name}/${m.index}/${m.name}`,
        ///                 [rest of string was truncated]&quot;;.
        /// </summary>
        public static string MacrosJsContent {
            get {
                return ResourceManager.GetString("MacrosJsContent", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // Chat filters plugin like for example NoSoliciting can help reduce noise when running commands
        ///
        ///// Collection name
        ///var ARGS = &apos;Self&apos;;
        ///
        ///function main(modsJson) {
        ///    const mods = JSON.parse(modsJson);
        ///
        ///    const macros = mods.flatMap(m =&gt; {
        ///        return m.settings.groupSettings.flatMap(s =&gt; {
        ///            var isMulti = s.type == &quot;Multi&quot;
        ///            const groupMacros = isMulti ? [{
        ///                name: `Clear Option Group [${s.name}]`,
        ///                path: `Mods/${m.path}/Settings/${escape(s [rest of string was truncated]&quot;;.
        /// </summary>
        public static string ModOptionsJsContent {
            get {
                return ResourceManager.GetString("ModOptionsJsContent", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // Chat filters plugin like for example NoSoliciting can help reduce noise when running commands
        ///
        ///// Collection name
        ///var ARGS = &apos;Self&apos;;
        ///
        ///function main(modsJson) {
        ///    const mods = JSON.parse(modsJson);
        ///    const macros = mods.flatMap(m =&gt; {
        ///        return [{
        ///            name: `Enable Mod [${m.name}]`,
        ///            path: `Mods/${normalize(m.path)}/Enable`,
        ///            tags: m.localTags.concat([&apos;mod&apos;, &apos;enable&apos;]),
        ///            args: ARGS,
        ///            content: `/penumbra mod enable {0} | ${m.dir}`
        ///  [rest of string was truncated]&quot;;.
        /// </summary>
        public static string ModsJsContent {
            get {
                return ResourceManager.GetString("ModsJsContent", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // Target name
        ///var ARGS = &apos;self&apos;;
        ///
        ///function main(moodlesJson) {
        ///     var moodles = JSON.parse(moodlesJson);
        ///     var macros = moodles.map(m =&gt; {
        ///        return {
        ///            name: `Apply Moodle [${m.Item3}]`,
        ///            path: `Moodles/${m.Item3}/Apply`,
        ///            tags: [&apos;moodle&apos;, &apos;apply&apos;],
        ///            args: ARGS,
        ///            content: `/moodle apply {0} moodle &quot;${m.Item1}&quot;`
        ///        };
        ///     });
        ///     return JSON.stringify(macros);
        ///}
        ///.
        /// </summary>
        public static string MoodlesJsContent {
            get {
                return ResourceManager.GetString("MoodlesJsContent", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // Examples
        ///const TRANSFORMERS = [
        ///    // Assign custom commands for specific custom emotes
        ///    {match: m =&gt; m.path.includes(&apos;Remote Shock Collar [Mittens]&apos;) &amp;&amp; m.tags.includes(&apos;option&apos;) &amp;&amp; m.tags.includes(&apos;emote&apos;), mutate: m =&gt; m.command = `/shock${[&apos;/upset&apos;, &apos;/shocked&apos;, &apos;/sulk&apos;, &apos;/kneel&apos;].indexOf(m.tags.find(t =&gt; t.startsWith(&apos;/&apos;))) + 1}`},
        ///    {match: m =&gt; m.path.includes(&apos;Remote Vibrator [Mittens]&apos;) &amp;&amp; m.tags.includes(&apos;option&apos;) &amp;&amp; m.tags.includes(&apos;emote&apos;), mutate: m =&gt; m.command = `/vibrate${[&apos;/blus [rest of string was truncated]&quot;;.
        /// </summary>
        public static string OverridesJsContent {
            get {
                return ResourceManager.GetString("OverridesJsContent", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to function main(collectionNamesJson) {
        ///     var collectionNames = JSON.parse(collectionNamesJson);
        ///     var macros = collectionNames.flatMap(n =&gt; {
        ///        return [{
        ///            name: `Enable Plugin Collection [${n}]`,
        ///            path: `Plugins/Collections/${n}/Enable`,
        ///            tags: [&apos;plugin&apos;, &apos;collection&apos;, &apos;enable&apos;],
        ///            content: `/xlenablecollection &quot;${n}&quot;`
        ///        }, {
        ///            name: `Disable Plugin Collection [${n}]`,
        ///            path: `Plugins/Collections/${n}/Disable`,
        ///       [rest of string was truncated]&quot;;.
        /// </summary>
        public static string PluginCollectionsJsContent {
            get {
                return ResourceManager.GetString("PluginCollectionsJsContent", resourceCulture);
            }
        }
    }
}

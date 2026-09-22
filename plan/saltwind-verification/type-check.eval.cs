var asm = typeof(HighSeas.Tests.RunEncounterTrackerTests).Assembly;
var t = asm.GetType("HighSeas.Tests.SaltwindStageTimelineTests");
return t != null ? t.FullName : "missing";

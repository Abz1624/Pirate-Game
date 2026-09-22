var asm = typeof(HighSeas.Balance.StageBalanceProfile).Assembly;
var t = asm.GetType("HighSeas.Balance.SaltwindStageTimelineLogic");
return t != null ? "logic_ok" : "logic_missing";

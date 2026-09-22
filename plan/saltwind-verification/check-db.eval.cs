var dbPath = "Assets/_Game/Resources/GameDatabase.asset";
var db = UnityEditor.AssetDatabase.LoadAssetAtPath<HighSeas.Data.GameDatabase>(dbPath);
return db != null ? "db_ok" : "db_missing";

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class MahjongLogic
{
    // ==========================================================================================
    //  1. シャンテン数計算 (変更なし)
    // ==========================================================================================
    public static int CalculateShanten(int[] tiles, int meldCount = 0)
    {
        int minShanten = 8;
        if (meldCount == 0)
        {
            int kokushi = CalculateKokushiShanten(tiles);
            if (kokushi < minShanten) minShanten = kokushi;
            int chitoi = CalculateChitoitsuShanten(tiles);
            if (chitoi < minShanten) minShanten = chitoi;
        }
        int normal = CalculateNormalShanten(tiles, meldCount);
        if (normal < minShanten) minShanten = normal;

        return minShanten;
    }

    // ==========================================================================================
    //  2. 点数計算メインメソッド
    // ==========================================================================================

    public static ScoringResult CalculateScore(int[] tiles, List<int> melds, ScoringContext context)
    {
        List<ScoringResult> candidates = new List<ScoringResult>();

        // --- 国士無双チェック ---
        if (context.IsMenzen && CheckKokushi(tiles))
        {
            ScoringResult res = new ScoringResult();
            bool is13Wait = IsKokushi13Wait(tiles, context.WinningTileId);

            // 天和・地和チェック
            int yakumanHan = 0;
            if (CheckTenhouChiihou(res.YakuList, ref yakumanHan, context)) { }

            if (context.SpecialYakuTriggers != null)
            {
                if (context.SpecialYakuTriggers.Contains("大三元確定")) { yakumanHan += 13; res.YakuList.Add("大三元"); }
                if (context.SpecialYakuTriggers.Contains("九蓮宝燈確定")) { yakumanHan += 13; res.YakuList.Add("九蓮宝燈"); }
                if (context.SpecialYakuTriggers.Contains("大四喜確定")) { yakumanHan += 26; res.YakuList.Add("大四喜"); }
                if (context.SpecialYakuTriggers.Contains("緑一色確定")) { yakumanHan += 13; res.YakuList.Add("緑一色"); }
                if (context.SpecialYakuTriggers.Contains("字一色確定")) { yakumanHan += 13; res.YakuList.Add("字一色"); }
            }

            // ★修正: 13面待ちは2倍(26翻)、通常は1倍(13翻)としてベースを計算
            int baseKokushiHan = is13Wait ? 26 : 13;
            int totalYakumanHan = baseKokushiHan + yakumanHan;
            int multiplier = totalYakumanHan / 13;

            // ソート優先度のためHanは100倍にしておく（既存ロジック維持）
            res.Han = 100 * multiplier;

            // ★ここで「二倍役満」などの文字列を生成
            res.ScoreName = GetYakumanName(multiplier);

            if (is13Wait) res.YakuList.Add("国士無双13面待ち");
            else res.YakuList.Add("国士無双");

            CalculatePoints(res, context);
            res.DebugInfo = "国士無双";
            candidates.Add(res);

        }

        // --- 七対子チェック ---
        if (context.IsMenzen && CheckChiitoitsu(tiles))
        {
            ScoringResult chiitoiRes = new ScoringResult();
            CalculateChiitoiNormal(tiles, context, chiitoiRes);
            CalculatePoints(chiitoiRes, context);
            candidates.Add(chiitoiRes);
        }

        // --- 通常面子手チェック ---
        int meldCount = (melds != null) ? melds.Count / 4 : 0;
        var allStructures = DecomposeHandAllPatterns(tiles, meldCount);

        if (allStructures.Count > 0)
        {
            foreach (var structure in allStructures)
            {
                ScoringResult normalRes = new ScoringResult();
                CalculateNormalYaku(structure, tiles, melds, context, normalRes);
                CalculatePoints(normalRes, context);
                candidates.Add(normalRes);
            }
        }
        else if (candidates.Count == 0)
        {
            ScoringResult err = new ScoringResult();
            err.YakuList.Add("形式エラー(No Mentsu)");
            return err;
        }

        if (candidates.Count == 0)
        {
            ScoringResult err = new ScoringResult();
            err.YakuList.Add("役なし");
            err.DebugInfo = "No Yaku Found";
            return err;
        }

        var bestResult = candidates.OrderByDescending(r => r.TotalScore)
                                   .ThenByDescending(r => r.Han)
                                   .ThenByDescending(r => r.Fu)
                                   .First();

        if (bestResult.TotalScore == 0 && bestResult.YakuList.Count == 0)
        {
            bestResult.YakuList.Add("役なし");
        }

        return bestResult;
    }

    private static void CalculateNormalYaku(List<int> structure, int[] tiles, List<int> melds, ScoringContext context, ScoringResult result)
    {
        int han = 0;
        List<string> yakus = new List<string>();
        StringBuilder sb = new StringBuilder();

        List<string> yakumanList = new List<string>();
        int yakumanHan = 0;

        // --- 役満チェック ---
        if (CheckTenhouChiihou(yakumanList, ref yakumanHan, context))
        {
            Debug.Log("NormalYaku Route: Tenhou/Chiihou Detected");
        }
        // 変更前: if (CheckDaisangen(tiles, melds)) { yakumanHan += 13; yakumanList.Add("大三元"); }

        // 変更後: ギミックの確定フラグも一緒に見るようにする
        if (CheckDaisangen(tiles, melds) || (context.SpecialYakuTriggers != null && context.SpecialYakuTriggers.Contains("大三元確定")))
        {
            if (!yakumanList.Contains("大三元")) { yakumanHan += 13; yakumanList.Add("大三元"); }
        }
        

        if (context.IsMenzen)
        {
            int suuankouType = GetSuuankouType(structure, context);
            if (suuankouType == 2) { yakumanHan += 26; yakumanList.Add("四暗刻単騎待ち"); }
            else if (suuankouType == 1) { yakumanHan += 13; yakumanList.Add("四暗刻"); }
        }
        if (CheckTsuuissou(tiles, melds) || (context.SpecialYakuTriggers != null && context.SpecialYakuTriggers.Contains("字一色確定"))) { yakumanHan += 13; yakumanList.Add("字一色"); }
        if (CheckRyuuiisou(tiles, melds) || (context.SpecialYakuTriggers != null && context.SpecialYakuTriggers.Contains("緑一色確定"))) { yakumanHan += 13; yakumanList.Add("緑一色"); }
        if (CheckChinroutou(structure, melds)) { yakumanHan += 13; yakumanList.Add("清老頭"); }
        int sushiHan = CheckSuushi(tiles, melds);
        if (sushiHan == 2 || (context.SpecialYakuTriggers != null && context.SpecialYakuTriggers.Contains("大四喜確定"))) { yakumanHan += 26; yakumanList.Add("大四喜"); }
        else if (sushiHan == 1) { yakumanHan += 13; yakumanList.Add("小四喜"); }

        // 四槓子チェック (meldsがnullでない前提)
        int kanCount = (melds != null) ? melds.Count / 4 : 0;
        if (kanCount == 4) { yakumanHan += 13; yakumanList.Add("四槓子"); }

        if (context.IsMenzen && CheckChinitsu(tiles, melds))
        {
            int chuurenType = GetChuurenType(tiles, context.WinningTileId);
            if (chuurenType == 2) { yakumanHan += 26; yakumanList.Add("純正九蓮宝燈"); }
            else if (chuurenType == 1) { yakumanHan += 13; yakumanList.Add("九蓮宝燈"); }
        }
        // ★追加: ギミックで九蓮宝燈が確定している場合 (面子手が完成さえしていれば成立)
        if (context.SpecialYakuTriggers != null && context.SpecialYakuTriggers.Contains("九蓮宝燈確定"))
        {
            if (!yakumanList.Contains("純正九蓮宝燈") && !yakumanList.Contains("九蓮宝燈"))
            {
                yakumanHan += 13; yakumanList.Add("九蓮宝燈");
            }
        }
        // ★追加: 面子手であがった場合にも「国士無双確定」を乗せる
        if (context.SpecialYakuTriggers != null && context.SpecialYakuTriggers.Contains("国士無双確定"))
        {
            if (!yakumanList.Contains("国士無双")) { yakumanHan += 13; yakumanList.Add("国士無双"); }
        }
        // トリガー役満
        if (context.SpecialYakuTriggers != null)
        {
            if (context.SpecialYakuTriggers.Contains("真ん中強し"))
            {
                yakumanHan += 13;
                yakumanList.Add("真ん中強し");
            }
        }

        if (yakumanHan > 0)
        {
            int multiplier = yakumanHan / 13;
            result.Han = 100 * multiplier;
            result.ScoreName = GetYakumanName(multiplier);
            result.YakuList = yakumanList;
            result.DebugInfo = "Yakuman Detected!";
            return;
        }

        // --- 通常役チェック ---
        AddCommonYaku(yakus, ref han, context, structure);

        if (context.IsMenzen && (melds == null || melds.Count == 0) && CheckPinfu(structure, context))
        {
            han++; yakus.Add("平和");
        }

        if (CheckTanyao(tiles, melds)) { han++; yakus.Add("断幺九"); }

        if (context.IsMenzen && CheckIipeikou(structure)) { han++; yakus.Add("一盃口"); }

        han += CheckYakuhai(tiles, melds, context, yakus);

        if (CheckSanshokuDoujun(structure)) { han += (context.IsMenzen ? 2 : 1); yakus.Add("三色同順"); }
        if (CheckIttsu(structure)) { han += (context.IsMenzen ? 2 : 1); yakus.Add("一気通貫"); }
        if (CheckChanta(structure, melds))
        {
            if (CheckJunchan(structure, melds)) { han += (context.IsMenzen ? 3 : 2); yakus.Add("純全帯幺九"); }
            else { han += (context.IsMenzen ? 2 : 1); yakus.Add("混全帯幺九"); }
        }
        if (CheckToitoi(structure, melds)) { han += 2; yakus.Add("対々和"); }
        if (CheckSanankou(structure, melds, context)) { han += 2; yakus.Add("三暗刻"); }

        // --- ★修正: 三槓子判定追加 ---
        // meldsに4枚セットで格納されているものをすべて槓子とみなす前提のロジックです
        if (kanCount == 3) { han += 2; yakus.Add("三槓子"); }
        // -----------------------------

        if (CheckSanshokuDoukou(structure, melds)) { han += 2; yakus.Add("三色同刻"); }
        if (CheckHonroutou(structure, melds)) { han += 2; yakus.Add("混老頭"); }
        if (CheckShousangen(tiles, melds)) { han += 2; yakus.Add("小三元"); }

        if (CheckChinitsu(tiles, melds)) { han += (context.IsMenzen ? 6 : 5); yakus.Add("清一色"); }
        else if (CheckHonitsu(tiles, melds)) { han += (context.IsMenzen ? 3 : 2); yakus.Add("混一色"); }

        if (context.IsMenzen)
        {
            int iipeikouCount = CountIipeikou(structure);
            if (iipeikouCount >= 2)
            {
                if (yakus.Contains("一盃口")) { han--; yakus.Remove("一盃口"); }
                han += 3; yakus.Add("二盃口");
            }
        }

        int doraCount = CountTotalDora(tiles, melds, context, yakus, sb);
        han += doraCount;

        result.Han = han;
        result.YakuList = yakus;

        if (yakus.Contains("平和") && context.IsTsumo)
        {
            result.Fu = 20;
        }
        else
        {
            result.Fu = CalculateFu(structure, tiles, melds, context);
            if (yakus.Contains("平和") && !context.IsTsumo && result.Fu < 30)
            {
                result.Fu = 30;
            }
        }

        sb.AppendLine($"飜: {han}, 符: {result.Fu}, 役: {string.Join(",", yakus)}");
        result.DebugInfo = sb.ToString();
    }

    // ==========================================================================================
    //  3. 七対子 / 天和判定追加 (変更なし)
    // ==========================================================================================
    private static void CalculateChiitoiNormal(int[] tiles, ScoringContext context, ScoringResult result)
    {
        List<string> yakumanList = new List<string>();  
        int yakumanHan = 0;

        if (CheckTenhouChiihou(yakumanList, ref yakumanHan, context))
        {
            Debug.Log("Chiitoi Route: Tenhou/Chiihou Detected");
        }
        if (context.SpecialYakuTriggers != null)
        {
            Debug.Log("This is Chiitoi Route");
            foreach (var trigger in context.SpecialYakuTriggers)
            {
                Debug.Log($"Trigger: {trigger}");
            }
            if (context.SpecialYakuTriggers.Contains("真ん中強し"))
            {
                yakumanHan += 13;
                yakumanList.Add("真ん中強し");
            }
            // ★追加
            if (context.SpecialYakuTriggers.Contains("大三元確定") && !yakumanList.Contains("大三元"))
            {
                yakumanHan += 13;
                yakumanList.Add("大三元");
            }
            // ★追加
            if (context.SpecialYakuTriggers.Contains("九蓮宝燈確定") && !yakumanList.Contains("九蓮宝燈"))
            {
                yakumanHan += 13;
                yakumanList.Add("九蓮宝燈");
            }
            if (context.SpecialYakuTriggers.Contains("大四喜確定") && !yakumanList.Contains("大四喜"))
            {
                // 大四喜はダブル役満
                yakumanHan += 26;
                yakumanList.Add("大四喜");
            }
            if (context.SpecialYakuTriggers.Contains("緑一色確定") && !yakumanList.Contains("緑一色"))
            {
                yakumanHan += 13;
                yakumanList.Add("緑一色");
            }
            if (context.SpecialYakuTriggers.Contains("字一色確定") && !yakumanList.Contains("字一色"))
            {
                yakumanHan += 13;
                yakumanList.Add("字一色");
            }
            if (context.SpecialYakuTriggers.Contains("国士無双確定") && !yakumanList.Contains("国士無双"))
            {
                yakumanHan += 13;
                yakumanList.Add("国士無双");
            }
        }
        
        if (CheckTsuuissou(tiles, null) && !yakumanList.Contains("字一色")) { yakumanHan += 13; yakumanList.Add("字一色"); }

        if (yakumanHan > 0)
        {
            int multiplier = yakumanHan / 13;
            result.Han = 100 * multiplier;
            result.ScoreName = GetYakumanName(multiplier);
            result.YakuList = yakumanList;
            result.DebugInfo = "Chiitoi Yakuman!";
            return;
        }

        int han = 0;
        List<string> yakus = new List<string>();
        StringBuilder sb = new StringBuilder();

        AddCommonYaku(yakus, ref han, context, null);
        han += 2;
        yakus.Add("七対子");
        if (CheckTanyao(tiles, null)) { han++; yakus.Add("断幺九"); }

        bool isHonroutou = true;
        for (int i = 0; i < 34; i++) { if (tiles[i] == 2 && !IsYaochu(i)) isHonroutou = false; }
        if (isHonroutou) { han += 2; yakus.Add("混老頭"); }

        if (CheckChinitsu(tiles, null)) { han += 6; yakus.Add("清一色"); }
        else if (CheckHonitsu(tiles, null)) { han += 3; yakus.Add("混一色"); }

        int dora = CountTotalDora(tiles, null, context, yakus, sb);
        han += dora;

        result.Han = han;
        result.Fu = 25;
        result.YakuList = yakus;
        result.DebugInfo = "Chiitoitsu " + sb.ToString();
    }

    // ==========================================================================================
    //  補助メソッド
    // ==========================================================================================

    // CheckPinfu等は変更なし

    private static bool CheckPinfu(List<int> structure, ScoringContext ctx)
    {
        int headId = structure[0];
        if (headId >= 31) return false;
        if (headId == 27 + ctx.SeatWind) return false;
        if (headId == 27 + ctx.RoundWind) return false;

        for (int i = 1; i < structure.Count; i++)
        {
            if (structure[i] >= 1000) return false;
        }

        int winId = ctx.WinningTileId;
        bool isRyanmen = false;

        for (int i = 1; i < structure.Count; i++)
        {
            int startId = structure[i];

            if (winId == startId)
            {
                if (startId % 9 != 6) isRyanmen = true;
            }
            else if (winId == startId + 2)
            {
                if (startId % 9 != 0) isRyanmen = true;
            }
        }
        return isRyanmen;
    }

    private static List<List<int>> DecomposeHandAllPatterns(int[] tiles, int meldCount)
    {
        var allPatterns = new List<List<int>>();
        int[] workTiles = (int[])tiles.Clone();
        int targetGroupCount = 5 - meldCount;
        for (int i = 0; i < 34; i++)
        {
            if (workTiles[i] >= 2)
            {
                workTiles[i] -= 2;
                List<int> currentStructure = new List<int>();
                currentStructure.Add(i);
                SearchMentsu(workTiles, 0, currentStructure, allPatterns, targetGroupCount);
                workTiles[i] += 2;
            }
        }
        return allPatterns;
    }

    private static void SearchMentsu(int[] tiles, int currentIdx, List<int> currentStructure, List<List<int>> results, int targetGroupCount)
    {
        if (currentStructure.Count == targetGroupCount)
        {
            bool empty = true;
            for (int i = 0; i < 34; i++) if (tiles[i] > 0) { empty = false; break; }
            if (empty) results.Add(new List<int>(currentStructure));
            return;
        }
        if (currentIdx >= 34) return;
        if (tiles[currentIdx] == 0)
        {
            SearchMentsu(tiles, currentIdx + 1, currentStructure, results, targetGroupCount);
            return;
        }
        if (tiles[currentIdx] >= 3)
        {
            tiles[currentIdx] -= 3;
            currentStructure.Add(currentIdx + 1000);
            SearchMentsu(tiles, currentIdx, currentStructure, results, targetGroupCount);
            currentStructure.RemoveAt(currentStructure.Count - 1);
            tiles[currentIdx] += 3;
        }
        if (currentIdx < 27 && currentIdx % 9 < 7 && tiles[currentIdx] > 0 && tiles[currentIdx + 1] > 0 && tiles[currentIdx + 2] > 0)
        {
            tiles[currentIdx]--; tiles[currentIdx + 1]--; tiles[currentIdx + 2]--;
            currentStructure.Add(currentIdx);
            SearchMentsu(tiles, currentIdx, currentStructure, results, targetGroupCount);
            currentStructure.RemoveAt(currentStructure.Count - 1);
            tiles[currentIdx]++; tiles[currentIdx + 1]++; tiles[currentIdx + 2]++;
        }
    }

    private static int CalculateNormalShanten(int[] tiles, int meldCount)
    {
        int minSyanten = 8;
        for (int i = 0; i < 34; i++)
        {
            if (tiles[i] >= 2)
            {
                tiles[i] -= 2;
                int current = RunSearch(tiles, meldCount, 0, 0, true);
                if (current < minSyanten) minSyanten = current;
                tiles[i] += 2;
            }
        }
        int noHead = RunSearch(tiles, meldCount, 0, 0, false);
        if (noHead < minSyanten) minSyanten = noHead;
        return minSyanten;
    }

    private static int RunSearch(int[] tiles, int mentsu, int tatsu, int currentIdx, bool hasHead)
    {
        if (currentIdx >= 34)
        {
            if (mentsu + tatsu > 4) tatsu = 4 - mentsu;
            int headValue = hasHead ? 1 : 0;
            return 8 - (mentsu * 2) - tatsu - headValue;
        }
        int best = 8;
        if (tiles[currentIdx] >= 3)
        {
            tiles[currentIdx] -= 3;
            best = Math.Min(best, RunSearch(tiles, mentsu + 1, tatsu, currentIdx, hasHead));
            tiles[currentIdx] += 3;
        }
        if (currentIdx < 27 && tiles[currentIdx] > 0 && tiles[currentIdx + 1] > 0 && tiles[currentIdx + 2] > 0)
        {
            bool borderCheck = true;
            if (currentIdx % 9 > 6) borderCheck = false;
            if (borderCheck)
            {
                tiles[currentIdx]--; tiles[currentIdx + 1]--; tiles[currentIdx + 2]--;
                best = Math.Min(best, RunSearch(tiles, mentsu + 1, tatsu, currentIdx, hasHead));
                tiles[currentIdx]++; tiles[currentIdx + 1]++; tiles[currentIdx + 2]++;
            }
        }
        if (tiles[currentIdx] >= 2)
        {
            tiles[currentIdx] -= 2;
            best = Math.Min(best, RunSearch(tiles, mentsu, tatsu + 1, currentIdx, hasHead));
            tiles[currentIdx] += 2;
        }
        if (currentIdx < 27 && tiles[currentIdx] > 0 && tiles[currentIdx + 1] > 0)
        {
            bool borderCheck = true;
            if (currentIdx % 9 == 8) borderCheck = false;
            if (borderCheck)
            {
                tiles[currentIdx]--; tiles[currentIdx + 1]--;
                best = Math.Min(best, RunSearch(tiles, mentsu, tatsu + 1, currentIdx, hasHead));
                tiles[currentIdx]++; tiles[currentIdx + 1]++;
            }
        }
        if (currentIdx < 27 && tiles[currentIdx] > 0 && tiles[currentIdx + 2] > 0)
        {
            bool borderCheck = true;
            if (currentIdx % 9 > 6) borderCheck = false;
            if (borderCheck)
            {
                tiles[currentIdx]--; tiles[currentIdx + 2]--;
                best = Math.Min(best, RunSearch(tiles, mentsu, tatsu + 1, currentIdx, hasHead));
                tiles[currentIdx]++; tiles[currentIdx + 2]++;
            }
        }
        best = Math.Min(best, RunSearch(tiles, mentsu, tatsu, currentIdx + 1, hasHead));
        return best;
    }

    private static int CountTotalDora(int[] tiles, List<int> melds, ScoringContext ctx, List<string> yakuList, StringBuilder sb)
    {
        int totalDora = 0;
        int omote = 0;
        sb.Append(" [Omote Dora] ");
        foreach (var d in ctx.DoraTiles)
        {
            int count = tiles[d];
            if (melds != null) foreach (int m in melds) if (m == d) count++;
            omote += count;
            sb.Append($"ID{d}({count}) ");
        }
        sb.AppendLine($" -> {omote}");
        if (omote > 0) { totalDora += omote; yakuList.Add($"ドラ{omote}"); }

        if (ctx.IsRiichi && ctx.UraDoraTiles != null)
        {
            int ura = 0;
            sb.Append(" [Ura Dora] ");
            foreach (var d in ctx.UraDoraTiles)
            {
                int count = tiles[d];
                if (melds != null) foreach (int m in melds) if (m == d) count++;
                ura += count;
                sb.Append($"ID{d}({count}) ");
            }
            sb.AppendLine($" -> {ura}");
            if (ura > 0) { totalDora += ura; yakuList.Add($"裏ドラ{ura}"); }
        }
        if (ctx.RedDoraCount > 0) { totalDora += ctx.RedDoraCount; yakuList.Add($"赤ドラ {ctx.RedDoraCount}"); }
        if (ctx.NukiDoraCount > 0) { totalDora += ctx.NukiDoraCount; yakuList.Add($"抜きドラ {ctx.NukiDoraCount}"); }
        return totalDora;
    }

    // --- ★修正: 符計算ロジック ---
    private static int CalculateFu(List<int> structure, int[] tiles, List<int> melds, ScoringContext ctx)
    {
        int fu = 20; // 副底
        if (ctx.IsTsumo) fu += 2; // ツモ符
        else if (ctx.IsMenzen) fu += 10; // 門前加符 (ロンの場合)

        // 手牌内の刻子・槓子（structureはDecomposeHandAllPatternsで生成されたもの）
        for (int i = 1; i < structure.Count; i++)
        {
            int val = structure[i];
            if (val >= 1000) // 1000以上は刻子/槓子としてエンコードされている
            {
                int tileId = val - 1000;
                bool isAnkou = true;
                // ロンあがりで、かつその牌があがり牌の場合、明刻扱い
                if (!ctx.IsTsumo && tileId == ctx.WinningTileId) isAnkou = false;

                // 刻子の基本点: 暗刻4 / 明刻2
                int baseFu = isAnkou ? 4 : 2;
                if (IsYaochu(tileId)) baseFu *= 2;
                fu += baseFu;
            }
        }

        // 副露（melds）の符計算
        // ※このロジックは「meldsに入っているものはすべて暗槓子扱い(16/32)」という実装になっています。
        // 暗槓の場合は正しいですが、明槓やポンが含まれる場合は本来は分岐が必要です。
        // ここでは「暗槓子3つ」のケースでバグが出ないよう、ループ処理を堅牢にします。
        if (melds != null && melds.Count > 0)
        {
            // meldsは4つの整数で1セットと仮定 (例: [id, id, id, id] で槓子)
            int sets = melds.Count / 4;
            for (int i = 0; i < sets; i++)
            {
                // インデックス範囲チェックを追加
                if ((i * 4) < melds.Count)
                {
                    int tileId = melds[i * 4];
                    bool isYaochu = IsYaochu(tileId);
                    // 現状の仕様: 暗槓扱い (中張16 / 么九32)
                    // ※明槓の場合は本来 (8 / 16) ですが、ユーザーの申告に合わせて暗槓として計算します
                    fu += isYaochu ? 32 : 16;
                }
            }
        }

        // 雀頭の符
        int headId = structure[0];
        if (headId >= 31) fu += 2; // 三元牌
        if (headId == 27 + ctx.SeatWind) fu += 2; // 自風
        if (headId == 27 + ctx.RoundWind) fu += 2; // 場風
        // 連風牌（ダブ東など）の場合は+4にするルールもありますが、ここではそれぞれ+2として加算（合計+4）

        // 切り上げ処理
        fu = (int)Math.Ceiling(fu / 10.0) * 10;
        if (fu < 20) fu = 20; // 理論上ありえませんが安全策
        return fu;
    }

    // 引数を変更: bool isDealer -> ScoringContext ctx
    private static void CalculatePoints(ScoringResult res, ScoringContext ctx)
    {
        bool isDealer = ctx.IsDealer;
        bool isTsumo = ctx.IsTsumo;

        // ---------------------------------------------------------
        // 1. 役満 (13翻以上) の処理
        // ---------------------------------------------------------
        if (res.Han >= 13)
        {
            // 内部的に Yakuman 1つにつき 100翻として計算しているため
            int multiplier = 1;
            if (res.Han >= 100) multiplier = res.Han / 100;

            // 旧式(13翻単位)のデータ向けバックアップ判定。
            // 100翻単位(100,200,300...)を使っている現在値には適用しない。
            if (multiplier == 1 && res.Han >= 26 && res.Han < 100) multiplier = res.Han / 13;

            int yakumanBase = 8000 * multiplier;

            // 名称の決定 (1->役満, 2->二倍役満, 3->三倍役満...)
            res.ScoreName = GetYakumanName(multiplier);

            if (isTsumo)
            {
                res.PayRon = 0;
                if (isDealer)
                {
                    int payChild = RoundUp100(yakumanBase * 2);
                    res.PayTsumoChild = payChild;
                    res.PayTsumoDealer = 0;
                    res.TotalScore = payChild * 3;
                }
                else
                {
                    res.PayTsumoDealer = RoundUp100(yakumanBase * 2);
                    res.PayTsumoChild = RoundUp100(yakumanBase * 1);
                    res.TotalScore = res.PayTsumoDealer + res.PayTsumoChild * 2;
                }
            }
            else
            {
                res.PayTsumoChild = 0;
                res.PayTsumoDealer = 0;
                if (isDealer) res.PayRon = RoundUp100(yakumanBase * 6);
                else res.PayRon = RoundUp100(yakumanBase * 4);
                res.TotalScore = res.PayRon;
            }

            AddHonbaPoints(res, ctx);
            return;
        }
        

        // ---------------------------------------------------------
        // 2. 通常の手 (12翻以下) の処理
        // ---------------------------------------------------------
        if (res.Han <= 0) return;
        if (res.Fu < 20) res.Fu = 20;

        // 基本点の計算
        int basicPoints = res.Fu * (int)Math.Pow(2, 2 + res.Han);

        // 満貫打ち切り判定
        if (basicPoints > 2000 || res.Han >= 5)
        {
            if (res.Han >= 11)      { basicPoints = 6000; res.ScoreName = "三倍満"; }
            else if (res.Han >= 8)  { basicPoints = 4000; res.ScoreName = "倍満"; }
            else if (res.Han >= 6)  { basicPoints = 3000; res.ScoreName = "跳満"; }
            else                    { basicPoints = 2000; res.ScoreName = "満貫"; }
        }

        // ---------------------------------------------------------
        // 3. 支払い額の決定 (ツモ/ロン分岐)
        // ---------------------------------------------------------
        if (isTsumo)
        {
            // === ツモアガリ ===
            res.PayRon = 0;

            if (isDealer)
            {
                // 親ツモ
                int payChild = RoundUp100(basicPoints * 2);
                res.PayTsumoChild = payChild;
                res.PayTsumoDealer = 0;
                res.TotalScore = payChild * 3;
            }
            else
            {
                // 子ツモ
                res.PayTsumoDealer = RoundUp100(basicPoints * 2);
                res.PayTsumoChild = RoundUp100(basicPoints * 1);
                res.TotalScore = res.PayTsumoDealer + res.PayTsumoChild * 2;
            }
        }
        else
        {
            // === ロンアガリ ===
            res.PayTsumoDealer = 0;
            res.PayTsumoChild = 0;

            if (isDealer) res.PayRon = RoundUp100(basicPoints * 6);
            else res.PayRon = RoundUp100(basicPoints * 4);
            
            res.TotalScore = res.PayRon;
        }

        // ★最後に本場加算処理を実行
        AddHonbaPoints(res, ctx);
    }

    private static string GetYakumanName(int multiplier)
    {
        if (multiplier <= 1) return "役満";

        // 数字を日本語に変換する簡易的な方法
        string[] kanjiNumbers = { "", "一", "二", "三", "四", "五", "六", "七", "八" };
        if (multiplier < kanjiNumbers.Length)
        {
            return kanjiNumbers[multiplier] + "倍役満";
        }
        return multiplier + "倍役満"; // 9倍以上の場合は数字
    }

    // ==========================================================================================
    //  ★追加: 本場加算処理 (1本場につき300点)
    // ==========================================================================================
    private static void AddHonbaPoints(ScoringResult res, ScoringContext ctx)
    {
        // 本場数が0なら何もしない
        if (ctx.HonbaCount <= 0) return;

        int honbaTotal = ctx.HonbaCount * 300; // 場に支払われる合計点 (300点 x 本場)
        int honbaSplit = ctx.HonbaCount * 100; // ツモの時の1人あたりの支払い (100点 x 本場)

        if (ctx.IsTsumo)
        {
            // ツモの場合: 全員から100点×本場数をもらう
            if (ctx.IsDealer)
            {
                // 親のツモ: 子全員が支払う
                res.PayTsumoChild += honbaSplit;
            }
            else
            {
                // 子のツモ: 親と子がそれぞれ支払う
                res.PayTsumoDealer += honbaSplit;
                res.PayTsumoChild += honbaSplit;
            }
            // 総得点に300点×本場数を加算
            res.TotalScore += honbaTotal;
        }
        else
        {
            // ロンの場合: 放銃者が300点×本場数をまとめて支払う
            res.PayRon += honbaTotal;
            res.TotalScore += honbaTotal;
        }
        
        // デバッグ情報に追記
        res.DebugInfo += $" (本場{ctx.HonbaCount}: +{honbaTotal})";
    }
    private static int RoundUp100(int value) { return (int)Math.Ceiling(value / 100.0) * 100; }

    // その他の補助メソッド（変更なし）
    public static List<int> GetWinningTiles(int[] tiles13, int meldCount = 0)
    {
        List<int> winningTiles = new List<int>();
        for (int i = 0; i < 34; i++)
        {
            tiles13[i]++;
            int shanten = CalculateShanten(tiles13, meldCount);
            if (shanten == -1) winningTiles.Add(i);
            tiles13[i]--;
        }
        return winningTiles;
    }
    // ... (以下の補助メソッドは元のファイルと同じ内容を維持してください)
    // GetAnkanCandidates, IsYaochu, IsHonor, CalculateKokushiShanten, etc.
    // ...
    public static List<int> GetAnkanCandidates(int[] tileCounts)
    {
        List<int> candidates = new List<int>();
        for (int i = 0; i < 34; i++) if (tileCounts[i] == 4) candidates.Add(i);
        return candidates;
    }

    private static bool IsYaochu(int id) { return (id < 27 && (id % 9 == 0 || id % 9 == 8)) || (id >= 27); }
    private static bool IsHonor(int id) { return id >= 27; }

    private static int CalculateKokushiShanten(int[] tiles) { int[] y = { 0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33 }; int t = 0; bool p = false; foreach (int i in y) { if (tiles[i] > 0) { t++; if (tiles[i] >= 2) p = true; } } return 13 - t - (p ? 1 : 0); }
    private static int CalculateChitoitsuShanten(int[] tiles) { int p = 0, k = 0; for (int i = 0; i < 34; i++) { if (tiles[i] > 0) { k++; if (tiles[i] >= 2) p++; } } int s = 6 - p; if (k < 7) s += (7 - k); return s; }
    private static int GetSuuankouType(List<int> s, ScoringContext c) { for (int i = 1; i < s.Count; i++) if (s[i] < 1000) return 0; return (s[0] == c.WinningTileId) ? 2 : (c.IsTsumo ? 1 : 0); }
    private static bool IsKokushi13Wait(int[] tiles, int winningTile) { if (winningTile < 0 || winningTile >= 34) return false; if (tiles[winningTile] < 1) return false; int[] checkTiles = (int[])tiles.Clone(); checkTiles[winningTile]--; int[] yaochuIds = { 0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33 }; foreach (int id in yaochuIds) { if (checkTiles[id] != 1) return false; } return true; }
    private static int GetChuurenType(int[] tiles, int winningTileId) { int[] counts = new int[9]; int suitOffset = -1; if (tiles[0] > 0 || tiles[8] > 0) suitOffset = 0; else if (tiles[9] > 0 || tiles[17] > 0) suitOffset = 9; else if (tiles[18] > 0 || tiles[26] > 0) suitOffset = 18; if (suitOffset == -1) return 0; for (int i = 0; i < 9; i++) counts[i] = tiles[suitOffset + i]; int winIndex = winningTileId - suitOffset; if (winIndex < 0 || winIndex > 8) return 0; counts[winIndex]--; bool isPureBase = true; if (counts[0] < 3) isPureBase = false; if (counts[8] < 3) isPureBase = false; for (int i = 1; i <= 7; i++) if (counts[i] < 1) isPureBase = false; counts[winIndex]++; if (isPureBase) return 2; bool isChuuren = true; if (counts[0] < 3) isChuuren = false; if (counts[8] < 3) isChuuren = false; for (int i = 1; i <= 7; i++) if (counts[i] < 1) isChuuren = false; if (isChuuren) return 1; return 0; }
    private static bool CheckKokushi(int[] t) { return CalculateKokushiShanten(t) == -1; }
    private static bool CheckChiitoitsu(int[] t) { return CalculateChitoitsuShanten(t) == -1; }
    private static bool CheckTenhouChiihou(List<string> l, ref int h, ScoringContext c) { if (c.IsFirstTurn && c.IsTsumo) { h += 13; l.Add(c.IsDealer ? "天和" : "地和"); return true; } return false; }
    private static bool CheckDaisangen(int[] t, List<int> m) { return HasTriplet(t, m, 31) && HasTriplet(t, m, 32) && HasTriplet(t, m, 33); }
    private static bool CheckTsuuissou(int[] t, List<int> m) { for (int i = 0; i < 27; i++) { if (t[i] > 0) return false; } if (m != null) { foreach (int id in m) { if (id < 27) return false; } } return true; }
    private static bool CheckRyuuiisou(int[] t, List<int> m) { int[] g = { 19, 20, 21, 23, 25, 32 }; for (int i = 0; i < 34; i++) { if (t[i] > 0 && !Array.Exists(g, x => x == i)) return false; } if (m != null) foreach (int x in m) if (!Array.Exists(g, v => v == x)) return false; return true; }
    private static bool CheckChinroutou(List<int> s, List<int> m) { if (!CheckHonroutou(s, m)) return false; foreach (int val in s) { int id = (val >= 1000) ? val - 1000 : val; if (IsHonor(id)) return false; } if (m != null) { foreach (int id in m) { if (IsHonor(id)) return false; } } return true; }
    private static int CheckSuushi(int[] t, List<int> m) { int tr = 0, pr = 0; for (int i = 27; i <= 30; i++) { if (HasTriplet(t, m, i)) tr++; else if (t[i] >= 2) pr++; } if (tr == 4) return 2; if (tr == 3 && pr == 1) return 1; return 0; }
    private static bool CheckChinitsu(int[] t, List<int> m) { return CheckFlush(t, m, false); }
    private static bool CheckHonitsu(int[] t, List<int> m) { return CheckFlush(t, m, true); }
    private static bool CheckFlush(int[] tiles, List<int> melds, bool allowHonors) { bool hasMan = false, hasPin = false, hasSou = false, hasHonor = false; for (int i = 0; i < 34; i++) { if (tiles[i] > 0) { if (i <= 8) hasMan = true; else if (i <= 17) hasPin = true; else if (i <= 26) hasSou = true; else hasHonor = true; } } if (melds != null) { foreach (int id in melds) { if (id <= 8) hasMan = true; else if (id <= 17) hasPin = true; else if (id <= 26) hasSou = true; else hasHonor = true; } } if (!allowHonors && hasHonor) return false; int suitCount = (hasMan ? 1 : 0) + (hasPin ? 1 : 0) + (hasSou ? 1 : 0); return suitCount == 1; }
    private static void AddCommonYaku(List<string> l, ref int h, ScoringContext c, List<int> s) { if (c.IsDoubleRiichi) { h += 2; l.Add("ダブル立直"); } else if (c.IsRiichi) { h++; l.Add("立直"); } if (c.IsIppatsu) { h++; l.Add("一発"); } if (c.IsRinshan) { h++; l.Add("嶺上開花"); } if (c.IsTsumo && c.IsHaitei) { h++; l.Add("海底撈月"); } if (!c.IsTsumo && c.IsHoutei) { h++; l.Add("河底撈魚"); } if (c.IsMenzen && c.IsTsumo) { h++; l.Add("門前清自摸和"); } if (c.IsChankan) { h++; l.Add("槍槓"); } }
    private static bool CheckTanyao(int[] t, List<int> m) { int[] y = { 0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33 }; for (int i = 0; i < 34; i++) if (t[i] > 0 && IsYaochu(i)) return false; if (m != null) foreach (int id in m) if (IsYaochu(id)) return false; return true; }
    private static bool CheckIipeikou(List<int> s) { var l = new List<int>(); for (int i = 1; i < s.Count; i++) if (s[i] < 1000) l.Add(s[i]); return l.GroupBy(x => x).Any(g => g.Count() >= 2); }
    private static int CountIipeikou(List<int> s) { var l = new List<int>(); for (int i = 1; i < s.Count; i++) if (s[i] < 1000) l.Add(s[i]); return l.GroupBy(x => x).Sum(g => g.Count() / 2); }
    private static int CheckYakuhai(int[] t, List<int> m, ScoringContext c, List<string> l) { int h = 0; if (HasTriplet(t, m, 31)) { h++; l.Add("役牌 白"); } if (HasTriplet(t, m, 32)) { h++; l.Add("役牌 發"); } if (HasTriplet(t, m, 33)) { h++; l.Add("役牌 中"); } int rw = 27 + c.RoundWind; if (HasTriplet(t, m, rw)) { h++; l.Add("場風牌"); } int sw = 27 + c.SeatWind; if (HasTriplet(t, m, sw)) { h++; l.Add("自風牌"); } return h; }
    private static bool HasTriplet(int[] t, List<int> m, int id) { if (t[id] >= 3) return true; if (m != null) { int c = 0; foreach (int x in m) if (x == id) c++; if (c >= 3) return true; } return false; }
    private static bool CheckSanshokuDoujun(List<int> s) { List<int> shuntsu = new List<int>(); for (int i = 1; i < s.Count; i++) if (s[i] < 1000) shuntsu.Add(s[i]); foreach (int val in shuntsu) { if (val >= 9) continue; if (shuntsu.Contains(val + 9) && shuntsu.Contains(val + 18)) return true; } return false; }
    private static bool CheckIttsu(List<int> s) { List<int> shuntsu = new List<int>(); for (int i = 1; i < s.Count; i++) if (s[i] < 1000) shuntsu.Add(s[i]); int[] starts = { 0, 9, 18 }; foreach (int st in starts) { if (shuntsu.Contains(st) && shuntsu.Contains(st + 3) && shuntsu.Contains(st + 6)) return true; } return false; }
    private static bool CheckChanta(List<int> s, List<int> m) { if (!IsYaochu(s[0])) return false; for (int i = 1; i < s.Count; i++) { int v = s[i]; if (v >= 1000) { if (!IsYaochu(v - 1000)) return false; } else { if (v % 9 != 0 && v % 9 != 6) return false; } } if (m != null) foreach (int id in m) if (!IsYaochu(id)) return false; return true; }
    private static bool CheckJunchan(List<int> s, List<int> m) { if (!CheckChanta(s, m)) return false; if (IsHonor(s[0])) return false; for (int i = 1; i < s.Count; i++) { int v = s[i]; int id = (v >= 1000) ? v - 1000 : v; if (IsHonor(id)) return false; } if (m != null) foreach (int id in m) if (IsHonor(id)) return false; return true; }
    private static bool CheckToitoi(List<int> s, List<int> m) { for (int i = 1; i < s.Count; i++) if (s[i] < 1000) return false; return true; }
    private static bool CheckSanankou(List<int> s, List<int> m, ScoringContext c) { int ac = 0; for (int i = 1; i < s.Count; i++) { if (s[i] >= 1000) { int id = s[i] - 1000; if (!c.IsTsumo && id == c.WinningTileId) continue; ac++; } } if (m != null) ac += m.Count / 4; return ac >= 3; }
    private static bool CheckSanshokuDoukou(List<int> s, List<int> m) { List<int> koutsu = new List<int>(); for (int i = 1; i < s.Count; i++) if (s[i] >= 1000) koutsu.Add(s[i] - 1000); if (m != null) foreach (int x in m) koutsu.Add(x); foreach (int k in koutsu) { if (k >= 9) continue; if (koutsu.Contains(k + 9) && koutsu.Contains(k + 18)) return true; } return false; }
    private static bool CheckHonroutou(List<int> s, List<int> m)
    {
        // 1. 雀頭のチェック (Index 0 は雀頭のID)
        if (!IsYaochu(s[0])) return false;

        // 2. 面子のチェック (Index 1 以降)
        for (int i = 1; i < s.Count; i++)
        {
            int val = s[i];

            // ★修正ポイント: 値が1000未満なら「順子」なので、混老頭は不成立
            // (たとえ 1-2-3 であっても 2 が含まれるためNG)
            if (val < 1000) return false;

            // 1000以上なら刻子/槓子。その牌が么九牌かチェック
            int id = val - 1000;
            if (!IsYaochu(id)) return false;
        }

        // 3. 副露(melds)のチェック
        if (m != null) 
        {
            foreach (int x in m) 
            {
                if (!IsYaochu(x)) return false;
            }
        }
        
        return true;
    }
    private static bool CheckShousangen(int[] t, List<int> m) { int tr = 0, pr = 0; for (int i = 31; i <= 33; i++) { if (HasTriplet(t, m, i)) tr++; else if (t[i] >= 2) pr++; } return (tr == 2 && pr == 1); }
    public static List<int> GetEffectiveTiles(int[] tiles13, int meldCount) { List<int> effectiveTiles = new List<int>(); int currentShanten = CalculateShanten(tiles13, meldCount); for (int i = 0; i < 34; i++) { tiles13[i]++; int nextShanten = CalculateShanten(tiles13, meldCount); tiles13[i]--; if (nextShanten < currentShanten) { effectiveTiles.Add(i); } } return effectiveTiles; }
}
/*
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class MahjongLogic
{
    // ==========================================================================================
    //  1. シャンテン数計算 (変更なし)
    // ==========================================================================================
    public static int CalculateShanten(int[] tiles, int meldCount = 0)
    {
        int minShanten = 8;
        if (meldCount == 0)
        {
            int kokushi = CalculateKokushiShanten(tiles);
            if (kokushi < minShanten) minShanten = kokushi;
            int chitoi = CalculateChitoitsuShanten(tiles);
            if (chitoi < minShanten) minShanten = chitoi;
        }
        int normal = CalculateNormalShanten(tiles, meldCount);
        if (normal < minShanten) minShanten = normal;

        return minShanten;
    }

    // ==========================================================================================
    //  2. 点数計算メインメソッド
    // ==========================================================================================
    
    public static ScoringResult CalculateScore(int[] tiles, List<int> melds, ScoringContext context)
    {
        List<ScoringResult> candidates = new List<ScoringResult>();

        // --- 国士無双チェック ---
        if (context.IsMenzen && CheckKokushi(tiles))
        {
            ScoringResult res = new ScoringResult();
            bool is13Wait = IsKokushi13Wait(tiles, context.WinningTileId);
            
            // 天和・地和チェック
            int yakumanHan = 0;
            if (CheckTenhouChiihou(res.YakuList, ref yakumanHan, context)) { }

            if (is13Wait) 
            { 
                // ダブル役満級は200翻として扱う（通常役より優先するため）
                res.Han = 200 + yakumanHan; 
                res.ScoreName = "二倍役満"; 
                res.YakuList.Add("国士無双13面待ち"); 
            }
            else 
            { 
                // シングル役満級は100翻として扱う
                res.Han = 100 + yakumanHan; 
                res.ScoreName = "役満"; 
                res.YakuList.Add("国士無双"); 
            }
            
            CalculatePoints(res, context);
            res.DebugInfo = "国士無双";
            candidates.Add(res); // 候補に追加
        }

        // --- 七対子チェック ---
        if (context.IsMenzen && CheckChiitoitsu(tiles))
        {
            ScoringResult chiitoiRes = new ScoringResult();
            CalculateChiitoiNormal(tiles, context, chiitoiRes);
            CalculatePoints(chiitoiRes, context);
            candidates.Add(chiitoiRes);
        }

        // --- 通常面子手チェック ---
        int meldCount = (melds != null) ? melds.Count / 4 : 0;
        var allStructures = DecomposeHandAllPatterns(tiles, meldCount);

        if (allStructures.Count > 0)
        {
            foreach (var structure in allStructures)
            {
                ScoringResult normalRes = new ScoringResult();
                CalculateNormalYaku(structure, tiles, melds, context, normalRes);
                CalculatePoints(normalRes, context);
                candidates.Add(normalRes);
            }
        }
        else if (candidates.Count == 0)
        {
            // 面子手分解できず、国士や七対子でもない場合
            ScoringResult err = new ScoringResult();
            err.YakuList.Add("形式エラー(No Mentsu)");
            return err;
        }

        if (candidates.Count == 0)
        {
            ScoringResult err = new ScoringResult();
            err.YakuList.Add("役なし");
            err.DebugInfo = "No Yaku Found";
            return err;
        }

        // --- 最も高い点数の結果を採用 ---
        // Hanの大きさで降順ソートすることで、意図的に大きくした役満(100翻~)を優先させる
        var bestResult = candidates.OrderByDescending(r => r.TotalScore)
                                   .ThenByDescending(r => r.Han) 
                                   .ThenByDescending(r => r.Fu)
                                   .First();

        if (bestResult.TotalScore == 0 && bestResult.YakuList.Count == 0)
        {
            bestResult.YakuList.Add("役なし");
        }

        return bestResult;
    }

    private static void CalculateNormalYaku(List<int> structure, int[] tiles, List<int> melds, ScoringContext context, ScoringResult result)
    {
        int han = 0;
        List<string> yakus = new List<string>();
        StringBuilder sb = new StringBuilder(); 

        List<string> yakumanList = new List<string>();
        int yakumanHan = 0; // 内部計算用の役満倍数（13単位）

        // --- 役満チェック ---
        // 天和・地和
        if (CheckTenhouChiihou(yakumanList, ref yakumanHan, context)) 
        {
            Debug.Log("NormalYaku Route: Tenhou/Chiihou Detected");
        }
        
        if (CheckDaisangen(tiles, melds)) { yakumanHan += 13; yakumanList.Add("大三元"); }
        if (context.IsMenzen)
        {
            int suuankouType = GetSuuankouType(structure, context);
            if (suuankouType == 2) { yakumanHan += 26; yakumanList.Add("四暗刻単騎待ち"); }
            else if (suuankouType == 1) { yakumanHan += 13; yakumanList.Add("四暗刻"); }
        }
        if (CheckTsuuissou(tiles, melds)) { yakumanHan += 13; yakumanList.Add("字一色"); }
        if (CheckRyuuiisou(tiles, melds)) { yakumanHan += 13; yakumanList.Add("緑一色"); }
        if (CheckChinroutou(structure, melds)) { yakumanHan += 13; yakumanList.Add("清老頭"); }
        int sushiHan = CheckSuushi(tiles, melds);
        if (sushiHan == 2) { yakumanHan += 26; yakumanList.Add("大四喜"); }
        else if (sushiHan == 1) { yakumanHan += 13; yakumanList.Add("小四喜"); }
        if (melds != null && melds.Count / 4 == 4) { yakumanHan += 13; yakumanList.Add("四槓子"); }
        if (context.IsMenzen && CheckChinitsu(tiles, melds))
        {
            int chuurenType = GetChuurenType(tiles, context.WinningTileId);
            if (chuurenType == 2) { yakumanHan += 26; yakumanList.Add("純正九蓮宝燈"); } 
            else if (chuurenType == 1) { yakumanHan += 13; yakumanList.Add("九蓮宝燈"); }
        }

        // トリガー役満
        if (context.SpecialYakuTriggers != null)
        {
            if (context.SpecialYakuTriggers.Contains("真ん中強し"))
            {
                yakumanHan += 13;
                yakumanList.Add("真ん中強し");
            }
        }

        if (yakumanHan > 0)
        {
            // ★修正: 役満確定時は、Hanを「100 * (yakumanHan/13)」にして通常役（最大でも30翻程度）より強制的に大きくする
            // これにより、BestResult選択時に確実に役満が選ばれるようにする
            int multiplier = yakumanHan / 13;
            result.Han = 100 * multiplier;
            
            result.ScoreName = (result.Han >= 200) ? "ダブル役満" : "役満";
            result.YakuList = yakumanList;
            result.DebugInfo = "Yakuman Detected!";
            return; // ここでリターンして通常役計算を行わない
        }

        // --- 通常役チェック ---
        AddCommonYaku(yakus, ref han, context, structure);

        if (context.IsMenzen && (melds == null || melds.Count == 0) && CheckPinfu(structure, context))
        {
            han++; yakus.Add("平和");
        }

        if (CheckTanyao(tiles, melds)) { han++; yakus.Add("断幺九"); }

        if (context.IsMenzen && CheckIipeikou(structure)) { han++; yakus.Add("一盃口"); }

        han += CheckYakuhai(tiles, melds, context, yakus);

        if (CheckSanshokuDoujun(structure)) { han += (context.IsMenzen ? 2 : 1); yakus.Add("三色同順"); }
        if (CheckIttsu(structure)) { han += (context.IsMenzen ? 2 : 1); yakus.Add("一気通貫"); }
        if (CheckChanta(structure, melds))
        {
            if (CheckJunchan(structure, melds)) { han += (context.IsMenzen ? 3 : 2); yakus.Add("純全帯幺九"); }
            else { han += (context.IsMenzen ? 2 : 1); yakus.Add("混全帯幺九"); }
        }
        if (CheckToitoi(structure, melds)) { han += 2; yakus.Add("対々和"); }
        if (CheckSanankou(structure, melds, context)) { han += 2; yakus.Add("三暗刻"); }
        if (CheckSanshokuDoukou(structure, melds)) { han += 2; yakus.Add("三色同刻"); }
        if (CheckHonroutou(structure, melds)) { han += 2; yakus.Add("混老頭"); }
        if (CheckShousangen(tiles, melds)) { han += 2; yakus.Add("小三元"); }

        if (CheckChinitsu(tiles, melds)) { han += (context.IsMenzen ? 6 : 5); yakus.Add("清一色"); }
        else if (CheckHonitsu(tiles, melds)) { han += (context.IsMenzen ? 3 : 2); yakus.Add("混一色"); }

        if (context.IsMenzen)
        {
            int iipeikouCount = CountIipeikou(structure);
            if (iipeikouCount >= 2)
            {
                if (yakus.Contains("一盃口")) { han--; yakus.Remove("一盃口"); }
                han += 3; yakus.Add("二盃口");
            }
        }

        int doraCount = CountTotalDora(tiles, melds, context, yakus, sb);
        han += doraCount;

        result.Han = han;
        result.YakuList = yakus;

        if (yakus.Contains("平和") && context.IsTsumo)
        {
            result.Fu = 20;
        }
        else
        {
            result.Fu = CalculateFu(structure, tiles, melds, context);
            if (yakus.Contains("平和") && !context.IsTsumo && result.Fu < 30)
            {
                result.Fu = 30;
            }
        }

        sb.AppendLine($"飜: {han}, 符: {result.Fu}, 役: {string.Join(",", yakus)}");
        result.DebugInfo = sb.ToString();
    }

    // ==========================================================================================
    //  3. 七対子 / 天和判定追加
    // ==========================================================================================

    private static void CalculateChiitoiNormal(int[] tiles, ScoringContext context, ScoringResult result)
    {
        // ★修正: 七対子ルートでも天和・地和チェックを行う
        List<string> yakumanList = new List<string>();
        int yakumanHan = 0;
        
        // 天和・地和
        if (CheckTenhouChiihou(yakumanList, ref yakumanHan, context))
        {
            Debug.Log("Chiitoi Route: Tenhou/Chiihou Detected");
        }
        
        // トリガー役満
        if (context.SpecialYakuTriggers != null)
        {
            if (context.SpecialYakuTriggers.Contains("真ん中強し"))
            {
                yakumanHan += 13;
                yakumanList.Add("真ん中強し");
            }
        }
        
        // 字一色(七対子形)チェック
        if (CheckTsuuissou(tiles, null)) { yakumanHan += 13; yakumanList.Add("字一色"); }

        if (yakumanHan > 0)
        {
            // 役満成立時は強制的に高翻数にしてリターン
            int multiplier = yakumanHan / 13;
            result.Han = 100 * multiplier;
            result.ScoreName = (result.Han >= 200) ? "ダブル役満" : "役満";
            result.YakuList = yakumanList;
            result.DebugInfo = "Chiitoi Yakuman!";
            return;
        }

        // --- 以下、通常の七対子計算 ---
        int han = 2;
        List<string> yakus = new List<string> { "七対子" };
        StringBuilder sb = new StringBuilder();

        AddCommonYaku(yakus, ref han, context, null);
        if (CheckTanyao(tiles, null)) { han++; yakus.Add("断幺九"); }

        bool isHonroutou = true;
        for (int i = 0; i < 34; i++) { if (tiles[i] == 2 && !IsYaochu(i)) isHonroutou = false; }
        if (isHonroutou) { han += 2; yakus.Add("混老頭"); }

        if (CheckChinitsu(tiles, null)) { han += 6; yakus.Add("清一色"); }
        else if (CheckHonitsu(tiles, null)) { han += 3; yakus.Add("混一色"); }

        int dora = CountTotalDora(tiles, null, context, yakus, sb);
        han += dora;

        result.Han = han;
        result.Fu = 25; 
        result.YakuList = yakus;
        result.DebugInfo = "Chiitoitsu " + sb.ToString();
    }

    // ==========================================================================================
    //  補助メソッド
    // ==========================================================================================
    private static bool CheckPinfu(List<int> structure, ScoringContext ctx)
    {
        // 1. 雀頭の判定 (役牌・自風・場風はNG)
        int headId = structure[0];
        if (headId >= 31) return false; // 三元牌
        if (headId == 27 + ctx.SeatWind) return false; // 自風
        if (headId == 27 + ctx.RoundWind) return false; // 場風

        // 2. すべて順子(Shuntsu)であること (1000以上は刻子)
        for (int i = 1; i < structure.Count; i++)
        {
            if (structure[i] >= 1000) return false;
        }

        // 3. 両面待ち判定
        int winId = ctx.WinningTileId;
        bool isRyanmen = false;

        for (int i = 1; i < structure.Count; i++)
        {
            int startId = structure[i]; // 順子の最小数字のID
            
            // 当たり牌が順子の「端」かつ、それが数牌の端(1や9)を含まない待ちか
            // 例: [2,3,4] を 2 か 5 で待っている状態
            if (winId == startId) // [2,3,4] の 2 待ち
            {
                // 順子の左端が 3, 6, 9 (ID%9が2, 5, 8) 以外なら、右側(startId+3)が存在し得るため両面
                // ただし、ここでは「完成した形」から逆算するので
                // 「startId+2 が 9 (ID%9=8)」でない、かつ「winIdがその順子の左端」なら両面
                if (startId % 9 != 6) isRyanmen = true; 
            }
            else if (winId == startId + 2) // [2,3,4] の 4 待ち
            {
                // 順子の右端が 1, 4, 7 (ID%9=0, 3, 6) 以外なら両面
                if (startId % 9 != 0) isRyanmen = true;
            }
        }
        return isRyanmen;
    }

    // --- DecomposeHandAllPatterns, SearchMentsu, RunSearch, CountTotalDora, CalculateFu ---
    // (これらは元のロジックのままでOKですが、変更がないため省略します。元のファイルを維持してください)
    
    // 省略部分に必要なメソッドのシグネチャのみ記載（コンパイルエラー回避のため、実際は元のコードを維持）
    private static List<List<int>> DecomposeHandAllPatterns(int[] tiles, int meldCount) 
    {
        // 元のコードを維持
        var allPatterns = new List<List<int>>();
        int[] workTiles = (int[])tiles.Clone();
        int targetGroupCount = 5 - meldCount;
        for (int i = 0; i < 34; i++) {
            if (workTiles[i] >= 2) {
                workTiles[i] -= 2; 
                List<int> currentStructure = new List<int>();
                currentStructure.Add(i); 
                SearchMentsu(workTiles, 0, currentStructure, allPatterns, targetGroupCount);
                workTiles[i] += 2; 
            }
        }
        return allPatterns;
    }
    
    private static void SearchMentsu(int[] tiles, int currentIdx, List<int> currentStructure, List<List<int>> results, int targetGroupCount)
    {
         // 元のコードを維持
        if (currentStructure.Count == targetGroupCount) {
            bool empty = true;
            for (int i = 0; i < 34; i++) if (tiles[i] > 0) { empty = false; break; }
            if (empty) results.Add(new List<int>(currentStructure));
            return;
        }
        if (currentIdx >= 34) return;
        if (tiles[currentIdx] == 0) {
            SearchMentsu(tiles, currentIdx + 1, currentStructure, results, targetGroupCount);
            return;
        }
        if (tiles[currentIdx] >= 3) {
            tiles[currentIdx] -= 3;
            currentStructure.Add(currentIdx + 1000); 
            SearchMentsu(tiles, currentIdx, currentStructure, results, targetGroupCount); 
            currentStructure.RemoveAt(currentStructure.Count - 1);
            tiles[currentIdx] += 3;
        }
        if (currentIdx < 27 && currentIdx % 9 < 7 && tiles[currentIdx] > 0 && tiles[currentIdx + 1] > 0 && tiles[currentIdx + 2] > 0) {
            tiles[currentIdx]--; tiles[currentIdx + 1]--; tiles[currentIdx + 2]--;
            currentStructure.Add(currentIdx); 
            SearchMentsu(tiles, currentIdx, currentStructure, results, targetGroupCount); 
            currentStructure.RemoveAt(currentStructure.Count - 1);
            tiles[currentIdx]++; tiles[currentIdx + 1]++; tiles[currentIdx + 2]++;
        }
    }

    private static int CalculateNormalShanten(int[] tiles, int meldCount)
    {
        int minSyanten = 8;
        for (int i = 0; i < 34; i++)
        {
            if (tiles[i] >= 2)
            {
                tiles[i] -= 2;
                int current = RunSearch(tiles, meldCount, 0, 0, true);
                if (current < minSyanten) minSyanten = current;
                tiles[i] += 2;
            }
        }
        int noHead = RunSearch(tiles, meldCount, 0, 0, false);
        if (noHead < minSyanten) minSyanten = noHead;
        return minSyanten;
    }

    private static int RunSearch(int[] tiles, int mentsu, int tatsu, int currentIdx, bool hasHead)
    {
        if (currentIdx >= 34) {
            if (mentsu + tatsu > 4) tatsu = 4 - mentsu;
            int headValue = hasHead ? 1 : 0;
            return 8 - (mentsu * 2) - tatsu - headValue;
        }
        int best = 8;
        if (tiles[currentIdx] >= 3) {
            tiles[currentIdx] -= 3;
            best = Math.Min(best, RunSearch(tiles, mentsu + 1, tatsu, currentIdx, hasHead));
            tiles[currentIdx] += 3;
        }
        if (currentIdx < 27 && tiles[currentIdx] > 0 && tiles[currentIdx + 1] > 0 && tiles[currentIdx + 2] > 0) {
            bool borderCheck = true;
            if (currentIdx % 9 > 6) borderCheck = false;
            if (borderCheck) {
                tiles[currentIdx]--; tiles[currentIdx + 1]--; tiles[currentIdx + 2]--;
                best = Math.Min(best, RunSearch(tiles, mentsu + 1, tatsu, currentIdx, hasHead));
                tiles[currentIdx]++; tiles[currentIdx + 1]++; tiles[currentIdx + 2]++;
            }
        }
        if (tiles[currentIdx] >= 2) {
            tiles[currentIdx] -= 2;
            best = Math.Min(best, RunSearch(tiles, mentsu, tatsu + 1, currentIdx, hasHead));
            tiles[currentIdx] += 2;
        }
        if (currentIdx < 27 && tiles[currentIdx] > 0 && tiles[currentIdx + 1] > 0) {
            bool borderCheck = true;
            if (currentIdx % 9 == 8) borderCheck = false;
            if (borderCheck) {
                tiles[currentIdx]--; tiles[currentIdx + 1]--;
                best = Math.Min(best, RunSearch(tiles, mentsu, tatsu + 1, currentIdx, hasHead));
                tiles[currentIdx]++; tiles[currentIdx + 1]++;
            }
        }
        if (currentIdx < 27 && tiles[currentIdx] > 0 && tiles[currentIdx + 2] > 0) {
            bool borderCheck = true;
            if (currentIdx % 9 > 6) borderCheck = false;
            if (borderCheck) {
                tiles[currentIdx]--; tiles[currentIdx + 2]--;
                best = Math.Min(best, RunSearch(tiles, mentsu, tatsu + 1, currentIdx, hasHead));
                tiles[currentIdx]++; tiles[currentIdx + 2]++;
            }
        }
        best = Math.Min(best, RunSearch(tiles, mentsu, tatsu, currentIdx + 1, hasHead));
        return best;
    }

    private static int CountTotalDora(int[] tiles, List<int> melds, ScoringContext ctx, List<string> yakuList, StringBuilder sb)
    {
        // 元のコードを維持
        int totalDora = 0;
        int omote = 0;
        sb.Append(" [Omote Dora] ");
        foreach (var d in ctx.DoraTiles)
        {
            int count = tiles[d];
            if (melds != null) foreach (int m in melds) if (m == d) count++;
            omote += count;
            sb.Append($"ID{d}({count}) ");
        }
        sb.AppendLine($" -> {omote}");
        if (omote > 0) { totalDora += omote; yakuList.Add($"ドラ{omote}"); }

        if (ctx.IsRiichi && ctx.UraDoraTiles != null)
        {
            int ura = 0;
            sb.Append(" [Ura Dora] ");
            foreach (var d in ctx.UraDoraTiles)
            {
                int count = tiles[d];
                if (melds != null) foreach (int m in melds) if (m == d) count++;
                ura += count;
                sb.Append($"ID{d}({count}) ");
            }
            sb.AppendLine($" -> {ura}");
            if (ura > 0) { totalDora += ura; yakuList.Add($"裏ドラ{ura}"); }
        }
        if (ctx.RedDoraCount > 0) { totalDora += ctx.RedDoraCount; yakuList.Add($"赤ドラ {ctx.RedDoraCount}"); }
        if (ctx.NukiDoraCount > 0) { totalDora += ctx.NukiDoraCount; yakuList.Add($"抜きドラ {ctx.NukiDoraCount}"); }
        return totalDora;
    }

    private static int CalculateFu(List<int> structure, int[] tiles, List<int> melds, ScoringContext ctx)
    {
        // 元のコードを維持
        int fu = 20;
        if (ctx.IsTsumo) fu += 2;
        else if (ctx.IsMenzen) fu += 10;

        for (int i = 1; i < structure.Count; i++)
        {
            int val = structure[i];
            if (val >= 1000)
            {
                int tileId = val - 1000;
                bool isAnkou = true;
                if (!ctx.IsTsumo && tileId == ctx.WinningTileId) isAnkou = false;
                int baseFu = isAnkou ? 4 : 2;
                if (IsYaochu(tileId)) baseFu *= 2;
                fu += baseFu;
            }
        }
        if (melds != null)
        {
            int sets = melds.Count / 4;
            for (int i = 0; i < sets; i++)
            {
                int tileId = melds[i * 4];
                bool isYaochu = IsYaochu(tileId);
                fu += isYaochu ? 32 : 16;
            }
        }
        int headId = structure[0];
        if (headId >= 31) fu += 2;
        if (headId == 27 + ctx.SeatWind) fu += 2;
        if (headId == 27 + ctx.RoundWind) fu += 2;

        fu = (int)Math.Ceiling(fu / 10.0) * 10;
        if (fu < 20) fu = 20;
        return fu;
    }
    // 引数を変更: bool isDealer -> ScoringContext ctx
    private static void CalculatePoints(ScoringResult res, ScoringContext ctx)
    {
        bool isDealer = ctx.IsDealer;
        bool isTsumo = ctx.IsTsumo;

        // ---------------------------------------------------------
        // 1. 役満 (13翻以上) の処理
        // ---------------------------------------------------------
        if (res.Han >= 13) 
        {
            int multiplier = 1;
            if (res.Han >= 100) multiplier = res.Han / 100;

            int yakumanBase = 8000 * multiplier; 

            if (isTsumo)
            {
                // --- ツモアガリ ---
                res.PayRon = 0; // ロン支払いは発生しない

                if (isDealer)
                {
                    // 親のツモ: 16000オール (基本点x2)
                    int payChild = RoundUp100(yakumanBase * 2);
                    res.PayTsumoChild = payChild;
                    res.PayTsumoDealer = 0;
                    res.TotalScore = payChild * 3;
                }
                else
                {
                    // 子のツモ: 親16000, 子8000
                    res.PayTsumoDealer = RoundUp100(yakumanBase * 2);
                    res.PayTsumoChild = RoundUp100(yakumanBase * 1);
                    res.TotalScore = res.PayTsumoDealer + res.PayTsumoChild * 2;
                }
            }
            else
            {
                // --- ロンアガリ ---
                res.PayTsumoChild = 0;
                res.PayTsumoDealer = 0;

                if (isDealer)
                {
                    // 親のロン: 48000 (基本点x6)
                    res.PayRon = RoundUp100(yakumanBase * 6);
                }
                else
                {
                    // 子のロン: 32000 (基本点x4)
                    res.PayRon = RoundUp100(yakumanBase * 4);
                }
                res.TotalScore = res.PayRon;
            }
            return;
        }

        // ---------------------------------------------------------
        // 2. 通常の手 (12翻以下) の処理
        // ---------------------------------------------------------
        if (res.Han <= 0) return;
        if (res.Fu < 20) res.Fu = 20;

        // 基本点の計算
        int basicPoints = res.Fu * (int)Math.Pow(2, 2 + res.Han);

        // 満貫打ち切り判定
        if (basicPoints > 2000 || res.Han >= 5)
        {
            if (res.Han >= 11)      { basicPoints = 6000; res.ScoreName = "三倍満"; }
            else if (res.Han >= 8)  { basicPoints = 4000; res.ScoreName = "倍満"; }
            else if (res.Han >= 6)  { basicPoints = 3000; res.ScoreName = "跳満"; }
            else                    { basicPoints = 2000; res.ScoreName = "満貫"; }
        }

        // ---------------------------------------------------------
        // 3. 支払い額の決定 (ツモ/ロン分岐)
        // ---------------------------------------------------------
        if (isTsumo)
        {
            // === ツモアガリ ===
            res.PayRon = 0;

            if (isDealer)
            {
                // 親ツモ: 基本点×2 を3人から
                // 例: 30符3翻(960) -> 1920 -> 2000オール (合計6000)
                int payChild = RoundUp100(basicPoints * 2);
                res.PayTsumoChild = payChild;
                res.PayTsumoDealer = 0;
                res.TotalScore = payChild * 3;
            }
            else
            {
                // 子ツモ: 親は基本点×2、子は基本点×1
                res.PayTsumoDealer = RoundUp100(basicPoints * 2);
                res.PayTsumoChild = RoundUp100(basicPoints * 1);
                res.TotalScore = res.PayTsumoDealer + res.PayTsumoChild * 2;
            }
        }
        else
        {
            // === ロンアガリ ===
            res.PayTsumoDealer = 0;
            res.PayTsumoChild = 0;

            if (isDealer)
            {
                // 親ロン: 基本点×6
                // 例: 30符3翻(960) -> 5760 -> 5800
                res.PayRon = RoundUp100(basicPoints * 6);
            }
            else
            {
                // 子ロン: 基本点×4
                res.PayRon = RoundUp100(basicPoints * 4);
            }
            res.TotalScore = res.PayRon;
        }
    }
    private static int RoundUp100(int value) { return (int)Math.Ceiling(value / 100.0) * 100; }

    public static List<int> GetWinningTiles(int[] tiles13, int meldCount = 0)
    {
        List<int> winningTiles = new List<int>();
        for (int i = 0; i < 34; i++)
        {
            tiles13[i]++;
            int shanten = CalculateShanten(tiles13, meldCount);
            if (shanten == -1) winningTiles.Add(i);
            tiles13[i]--;
        }
        return winningTiles;
    }

    public static List<int> GetAnkanCandidates(int[] tileCounts)
    {
        List<int> candidates = new List<int>();
        for (int i = 0; i < 34; i++) if (tileCounts[i] == 4) candidates.Add(i);
        return candidates;
    }

    private static bool IsYaochu(int id) { return (id < 27 && (id % 9 == 0 || id % 9 == 8)) || (id >= 27); }
    private static bool IsHonor(int id) { return id >= 27; }

    private static int CalculateKokushiShanten(int[] tiles) { int[] y = { 0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33 }; int t = 0; bool p = false; foreach (int i in y) { if (tiles[i] > 0) { t++; if (tiles[i] >= 2) p = true; } } return 13 - t - (p ? 1 : 0); }
    private static int CalculateChitoitsuShanten(int[] tiles) { int p = 0, k = 0; for (int i = 0; i < 34; i++) { if (tiles[i] > 0) { k++; if (tiles[i] >= 2) p++; } } int s = 6 - p; if (k < 7) s += (7 - k); return s; }
    private static int GetSuuankouType(List<int> s, ScoringContext c) { for (int i = 1; i < s.Count; i++) if (s[i] < 1000) return 0; return (s[0] == c.WinningTileId) ? 2 : (c.IsTsumo ? 1 : 0); }

    private static bool IsKokushi13Wait(int[] tiles, int winningTile)
    {
        // 念のため入力チェック
        if (winningTile < 0 || winningTile >= 34) return false;
        if (tiles[winningTile] < 1) return false;

        // 配列の複製を作成して計算（元の配列を汚さないため）
        int[] checkTiles = (int[])tiles.Clone();

        // 手牌（上がり形）から、上がり牌を1枚抜く
        checkTiles[winningTile]--;

        // 国士無双の構成牌（13種）
        int[] yaochuIds = { 0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33 };

        // 上がり牌を抜いた状態で、13種類すべてが「ちょうど1枚ずつ」あるか確認
        // これが成立する場合、待ちは「全13種待ち」だったことになる
        foreach (int id in yaochuIds)
        {
            if (checkTiles[id] != 1) return false;
        }

        return true;
    }

    private static int GetChuurenType(int[] tiles, int winningTileId)
    {
        int[] counts = new int[9];
        int suitOffset = -1;
        if (tiles[0] > 0 || tiles[8] > 0) suitOffset = 0;
        else if (tiles[9] > 0 || tiles[17] > 0) suitOffset = 9;
        else if (tiles[18] > 0 || tiles[26] > 0) suitOffset = 18;
        if (suitOffset == -1) return 0;

        for (int i = 0; i < 9; i++) counts[i] = tiles[suitOffset + i];

        int winIndex = winningTileId - suitOffset;
        if (winIndex < 0 || winIndex > 8) return 0;

        counts[winIndex]--;
        bool isPureBase = true;
        if (counts[0] < 3) isPureBase = false;
        if (counts[8] < 3) isPureBase = false;
        for (int i = 1; i <= 7; i++) if (counts[i] < 1) isPureBase = false;
        counts[winIndex]++;
        if (isPureBase) return 2;

        bool isChuuren = true;
        if (counts[0] < 3) isChuuren = false;
        if (counts[8] < 3) isChuuren = false;
        for (int i = 1; i <= 7; i++) if (counts[i] < 1) isChuuren = false;
        if (isChuuren) return 1;

        return 0;
    }

    private static bool CheckKokushi(int[] t) { return CalculateKokushiShanten(t) == -1; }
    private static bool CheckChiitoitsu(int[] t) { return CalculateChitoitsuShanten(t) == -1; }
    
    // ★修正: 天和・地和の判定メソッド。YakumanHanを加算する仕様に統一。
    private static bool CheckTenhouChiihou(List<string> l, ref int h, ScoringContext c) 
    { 
        if (c.IsFirstTurn && c.IsTsumo) 
        { 
            h += 13; 
            l.Add(c.IsDealer ? "天和" : "地和"); 
            return true; 
        } 
        return false; 
    }
    
    private static bool CheckDaisangen(int[] t, List<int> m) { return HasTriplet(t, m, 31) && HasTriplet(t, m, 32) && HasTriplet(t, m, 33); }
private static bool CheckTsuuissou(int[] t, List<int> m) { 
    // 1. 手牌(tiles)の中に数牌(ID 0-26)が1枚でもあればNG
    for (int i = 0; i < 27; i++) {
        if (t[i] > 0) return false;
    }
    
    // 2. 副露・槓子(melds)の中に数牌(ID 0-26)が1枚でもあればNG
    if (m != null) {
        foreach (int id in m) {
            if (id < 27) return false; // ID 27未満は数牌なので字一色不成立
        }
    }
    
    return true; 
}
    private static bool CheckRyuuiisou(int[] t, List<int> m) { int[] g = { 19, 20, 21, 23, 25, 32 }; for (int i = 0; i < 34; i++) { if (t[i] > 0 && !Array.Exists(g, x => x == i)) return false; } if (m != null) foreach (int x in m) if (!Array.Exists(g, v => v == x)) return false; return true; }
    private static bool CheckChinroutou(List<int> s, List<int> m)
    {
        if (!CheckHonroutou(s, m)) return false;

        foreach (int val in s)
        {
            int id = (val >= 1000) ? val - 1000 : val;
            if (IsHonor(id)) return false; // 字牌があればNG
        }
        if (m != null)
        {
            foreach (int id in m)
            {
                if (IsHonor(id)) return false; // 字牌があればNG
            }
        }
        return true;
    }
    private static int CheckSuushi(int[] t, List<int> m) { int tr = 0, pr = 0; for (int i = 27; i <= 30; i++) { if (HasTriplet(t, m, i)) tr++; else if (t[i] >= 2) pr++; } if (tr == 4) return 2; if (tr == 3 && pr == 1) return 1; return 0; }
    private static bool CheckChinitsu(int[] t, List<int> m) { return CheckFlush(t, m, false); }
    private static bool CheckHonitsu(int[] t, List<int> m) { return CheckFlush(t, m, true); }

    private static bool CheckFlush(int[] tiles, List<int> melds, bool allowHonors)
    {
        bool hasMan = false, hasPin = false, hasSou = false, hasHonor = false;
        for (int i = 0; i < 34; i++)
        {
            if (tiles[i] > 0)
            {
                if (i <= 8) hasMan = true;
                else if (i <= 17) hasPin = true;
                else if (i <= 26) hasSou = true;
                else hasHonor = true;
            }
        }
        if (melds != null)
        {
            foreach (int id in melds)
            {
                if (id <= 8) hasMan = true;
                else if (id <= 17) hasPin = true;
                else if (id <= 26) hasSou = true;
                else hasHonor = true;
            }
        }
        if (!allowHonors && hasHonor) return false;
        int suitCount = (hasMan ? 1 : 0) + (hasPin ? 1 : 0) + (hasSou ? 1 : 0);
        return suitCount == 1;
    }

    private static void AddCommonYaku(List<string> l, ref int h, ScoringContext c, List<int> s) 
    { 
        if (c.IsDoubleRiichi) 
        {
            h += 2; l.Add("ダブル立直"); 
        } 
        else if (c.IsRiichi) 
        { h++; l.Add("立直"); 
        } 
        
        if (c.IsIppatsu) { h++; l.Add("一発"); } 
        if (c.IsRinshan) { h++; l.Add("嶺上開花"); } 
        if (c.IsTsumo && c.IsHaitei) { h++; l.Add("海底撈月"); } 
        if (!c.IsTsumo && c.IsHoutei) { h++; l.Add("河底撈魚"); } 
        if (c.IsMenzen && c.IsTsumo) { h++; l.Add("門前清自摸和"); } 
        if (c.IsChankan) { h++; l.Add("槍槓"); } 
    }
    private static bool CheckTanyao(int[] t, List<int> m) { int[] y = { 0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33 }; for (int i = 0; i < 34; i++) if (t[i] > 0 && IsYaochu(i)) return false; if (m != null) foreach (int id in m) if (IsYaochu(id)) return false; return true; }
    private static bool CheckIipeikou(List<int> s) { var l = new List<int>(); for (int i = 1; i < s.Count; i++) if (s[i] < 1000) l.Add(s[i]); return l.GroupBy(x => x).Any(g => g.Count() >= 2); }
    private static int CountIipeikou(List<int> s) { var l = new List<int>(); for (int i = 1; i < s.Count; i++) if (s[i] < 1000) l.Add(s[i]); return l.GroupBy(x => x).Sum(g => g.Count() / 2); }
    private static int CheckYakuhai(int[] t, List<int> m, ScoringContext c, List<string> l) { int h = 0; if (HasTriplet(t, m, 31)) { h++; l.Add("役牌 白"); } if (HasTriplet(t, m, 32)) { h++; l.Add("役牌 發"); } if (HasTriplet(t, m, 33)) { h++; l.Add("役牌 中"); } int rw = 27 + c.RoundWind; if (HasTriplet(t, m, rw)) { h++; l.Add("場風牌"); } int sw = 27 + c.SeatWind; if (HasTriplet(t, m, sw)) { h++; l.Add("自風牌"); } return h; }
    private static bool HasTriplet(int[] t, List<int> m, int id) { if (t[id] >= 3) return true; if (m != null) { int c = 0; foreach (int x in m) if (x == id) c++; if (c >= 3) return true; } return false; }

    private static bool CheckSanshokuDoujun(List<int> s)
    {
        List<int> shuntsu = new List<int>();
        for (int i = 1; i < s.Count; i++) if (s[i] < 1000) shuntsu.Add(s[i]);
        foreach (int val in shuntsu)
        {
            if (val >= 9) continue; 
            if (shuntsu.Contains(val + 9) && shuntsu.Contains(val + 18)) return true;
        }
        return false;
    }
    private static bool CheckIttsu(List<int> s)
    {
        List<int> shuntsu = new List<int>();
        for (int i = 1; i < s.Count; i++) if (s[i] < 1000) shuntsu.Add(s[i]);
        int[] starts = { 0, 9, 18 };
        foreach (int st in starts)
        {
            if (shuntsu.Contains(st) && shuntsu.Contains(st + 3) && shuntsu.Contains(st + 6)) return true;
        }
        return false;
    }
    private static bool CheckChanta(List<int> s, List<int> m)
    {
        if (!IsYaochu(s[0])) return false;
        for (int i = 1; i < s.Count; i++)
        {
            int v = s[i];
            if (v >= 1000) { if (!IsYaochu(v - 1000)) return false; }
            else { if (v % 9 != 0 && v % 9 != 6) return false; }
        }
        if (m != null) foreach (int id in m) if (!IsYaochu(id)) return false;
        return true;
    }
    private static bool CheckJunchan(List<int> s, List<int> m)
    {
        if (!CheckChanta(s, m)) return false;
        if (IsHonor(s[0])) return false;
        for (int i = 1; i < s.Count; i++)
        {
            int v = s[i];
            int id = (v >= 1000) ? v - 1000 : v;
            if (IsHonor(id)) return false;
        }
        if (m != null) foreach (int id in m) if (IsHonor(id)) return false;
        return true;
    }
    private static bool CheckToitoi(List<int> s, List<int> m)
    {
        for (int i = 1; i < s.Count; i++) if (s[i] < 1000) return false;
        return true;
    }
    private static bool CheckSanankou(List<int> s, List<int> m, ScoringContext c)
    {
        int ac = 0;
        for (int i = 1; i < s.Count; i++)
        {
            if (s[i] >= 1000)
            {
                int id = s[i] - 1000;
                if (!c.IsTsumo && id == c.WinningTileId) continue;
                ac++;
            }
        }
        if (m != null) ac += m.Count / 4; 
        return ac >= 3;
    }
    private static bool CheckSanshokuDoukou(List<int> s, List<int> m)
    {
        List<int> koutsu = new List<int>();
        for (int i = 1; i < s.Count; i++) if (s[i] >= 1000) koutsu.Add(s[i] - 1000);
        if (m != null) foreach (int x in m) koutsu.Add(x);
        foreach (int k in koutsu)
        {
            if (k >= 9) continue;
            if (koutsu.Contains(k + 9) && koutsu.Contains(k + 18)) return true;
        }
        return false;
    }
    private static bool CheckHonroutou(List<int> s, List<int> m)
    {
        for (int i = 0; i < s.Count; i++)
        {
            int v = s[i];
            int id = (v >= 1000) ? v - 1000 : v;
            if (!IsYaochu(id)) return false;
        }
        if (m != null) foreach (int x in m) if (!IsYaochu(x)) return false;
        return true;
    }
    private static bool CheckShousangen(int[] t, List<int> m)
    {
        int tr = 0, pr = 0;
        for (int i = 31; i <= 33; i++)
        {
            if (HasTriplet(t, m, i)) tr++;
            else if (t[i] >= 2) pr++;
        }
        return (tr == 2 && pr == 1);
    }
    public static List<int> GetEffectiveTiles(int[] tiles13, int meldCount)
    {
        List<int> effectiveTiles = new List<int>();
        
        // 現在のシャンテン数を計算
        int currentShanten = CalculateShanten(tiles13, meldCount);

        // 34種の牌すべてについて、1枚加えてシャンテン数が下がるか試す
        for (int i = 0; i < 34; i++)
        {
            // 既に4枚使い切っている牌は有効牌になり得ない（空想上はなるが、物理的に引けない）
            // ここでは純粋な牌理として、4枚使い切りチェックはあえてせず「形として有効か」を判定します
            
            tiles13[i]++;
            int nextShanten = CalculateShanten(tiles13, meldCount);
            tiles13[i]--;

            // シャンテン数が減る（例: 1シャンテン -> 0シャンテン(テンパイ)）
            // または、テンパイ(0) -> 上がり(-1) になる場合
            if (nextShanten < currentShanten)
            {
                effectiveTiles.Add(i);
            }
        }
        
        return effectiveTiles;
    }
}
*/

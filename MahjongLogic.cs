using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class MahjongLogic
{
    // ==========================================================================================
    //  1. シャンテン数計算
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
    // ==========================================================================================
    private static void CalculateNormalYaku(List<int> structure, int[] tiles, List<int> melds, ScoringContext context, ScoringResult result)
    {
        int han = 0;
        List<string> yakus = new List<string>();
        StringBuilder sb = new StringBuilder(); 

        List<string> yakumanList = new List<string>();
        int yakumanHan = 0;

        // --- 役満チェック ---
        if (CheckTenhouChiihou(yakumanList, ref yakumanHan, context)) { }
        if (CheckDaisangen(tiles, melds)) { yakumanHan += 13; yakumanList.Add("Dai San Gen"); }
        if (context.IsMenzen)
        {
            int suuankouType = GetSuuankouType(structure, context);
            if (suuankouType == 2) { yakumanHan += 26; yakumanList.Add("Su Anko単騎"); }
            else if (suuankouType == 1) { yakumanHan += 13; yakumanList.Add("Su Anko"); }
        }
        if (CheckTsuuissou(tiles, melds)) { yakumanHan += 13; yakumanList.Add("Tsu Iso"); }
        if (CheckRyuuiisou(tiles, melds)) { yakumanHan += 13; yakumanList.Add("Ryu Iso"); }
        if (CheckChinroutou(structure, melds)) { yakumanHan += 13; yakumanList.Add("Chin Ro To"); }
        int sushiHan = CheckSuushi(tiles, melds);
        if (sushiHan == 2) { yakumanHan += 26; yakumanList.Add("Dai Sushi"); }
        else if (sushiHan == 1) { yakumanHan += 13; yakumanList.Add("Sho Sushi"); }
        if (melds != null && melds.Count / 4 == 4) { yakumanHan += 13; yakumanList.Add("Su Kantsu四槓子"); }
        if (context.IsMenzen && CheckChinitsu(tiles, melds))
        {
            int chuurenType = GetChuurenType(tiles, context.WinningTileId);
            if (chuurenType == 2) { yakumanHan += 26; yakumanList.Add("純正九蓮宝燈"); }
            else if (chuurenType == 1) { yakumanHan += 13; yakumanList.Add("九蓮宝燈"); }
        }

        // ★追加: トリガーによる特別役満の判定
        if (context.SpecialYakuTriggers != null)
        {
            // 例: 左端が中なら役満とする場合
            if (context.SpecialYakuTriggers.Contains("CENTER_CHUN"))
            {
                yakumanHan += 13;
                yakumanList.Add("Man Naka TSUYOSHI");
                Debug.Log("Chun is Man Naka");
            }
            else Debug.Log("Chun Is not Man Naka");
        }

        if (yakumanHan > 0)
        {
            result.Han = yakumanHan;
            result.ScoreName = (yakumanHan >= 26) ? "ダブル役満" : "役満";
            result.YakuList = yakumanList;
            result.DebugInfo = "Yakuman!";
            return;
        }

        // --- 通常役チェック ---
        AddCommonYaku(yakus, ref han, context, structure);

        if (context.IsMenzen && (melds == null || melds.Count == 0) && CheckPinfu(structure, context))
        {
            han++; yakus.Add("Pinfu");
        }

        if (CheckTanyao(tiles, melds)) { han++; yakus.Add("Tanyao"); }

        if (context.IsMenzen && CheckIipeikou(structure)) { han++; yakus.Add("IPEKO"); }

        han += CheckYakuhai(tiles, melds, context, yakus);

        if (CheckSanshokuDoujun(structure)) { han += (context.IsMenzen ? 2 : 1); yakus.Add("Sanshoku Dojun"); }
        if (CheckIttsu(structure)) { han += (context.IsMenzen ? 2 : 1); yakus.Add("ITTSU"); }
        if (CheckChanta(structure, melds))
        {
            if (CheckJunchan(structure, melds)) { han += (context.IsMenzen ? 3 : 2); yakus.Add("JUNG CHANG"); }
            else { han += (context.IsMenzen ? 2 : 1); yakus.Add("CHANGTA"); }
        }
        if (CheckToitoi(structure, melds)) { han += 2; yakus.Add("TOITOI"); }
        if (CheckSanankou(structure, melds, context)) { han += 2; yakus.Add("San Anko"); }
        if (CheckSanshokuDoukou(structure, melds)) { han += 2; yakus.Add("Sanshoku Doko"); }
        if (CheckHonroutou(structure, melds)) { han += 2; yakus.Add("Hon ROTO"); }
        if (CheckShousangen(tiles, melds)) { han += 2; yakus.Add("Sho San Gen"); }

        if (CheckChinitsu(tiles, melds)) { han += (context.IsMenzen ? 6 : 5); yakus.Add("ChinItsu"); }
        else if (CheckHonitsu(tiles, melds)) { han += (context.IsMenzen ? 3 : 2); yakus.Add("HonItsu"); }

        if (context.IsMenzen)
        {
            int iipeikouCount = CountIipeikou(structure);
            if (iipeikouCount >= 2)
            {
                if (yakus.Contains("IPEKO")) { han--; yakus.Remove("IPEKO"); }
                han += 3; yakus.Add("RYANPEKO");
            }
        }

        int doraCount = CountTotalDora(tiles, melds, context, yakus, sb);
        han += doraCount;

        // ★追加: トリガーによる通常役(翻数アップ)の判定
        if (context.SpecialYakuTriggers != null)
        {
            // 例: 右端が白なら +2翻
            if (context.SpecialYakuTriggers.Contains("RIGHT_HAKU"))
            {
                han += 2;
                yakus.Add("White Border (白界)");
            }
            // 例: 索子染めハンド（実際の清一色とは別に追加ボーナスなど）
            if (context.SpecialYakuTriggers.Contains("ALL_SOZU_HAND"))
            {
                han += 1;
                yakus.Add("Forest Blessing (森の加護)");
            }
        }

        result.Han = han;
        result.YakuList = yakus;

        if (yakus.Contains("Pinfu") && context.IsTsumo)
        {
            result.Fu = 20;
        }
        else
        {
            result.Fu = CalculateFu(structure, tiles, melds, context);
            if (yakus.Contains("Pinfu") && !context.IsTsumo && result.Fu < 30)
            {
                result.Fu = 30;
            }
        }

        sb.AppendLine($"Han: {han}, Fu: {result.Fu}, Yaku: {string.Join(",", yakus)}");
        result.DebugInfo = sb.ToString();
    }
    public static ScoringResult CalculateScore(int[] tiles, List<int> melds, ScoringContext context)
    {
        List<ScoringResult> candidates = new List<ScoringResult>();

        if (context.IsMenzen && CheckKokushi(tiles))
        {
            ScoringResult res = new ScoringResult();
            bool is13Wait = IsKokushi13Wait(tiles, context.WinningTileId);
            if (is13Wait) { res.Han = 26; res.ScoreName = "double Yakuman"; res.YakuList.Add("Kokushi 13 Men"); }
            else { res.Han = 13; res.ScoreName = "Yakuman"; res.YakuList.Add("Kokushi Musou"); }
            CheckTenhouChiihou(res.YakuList, ref res.Han, context);
            CalculatePoints(res, context.IsDealer);
            res.DebugInfo = "Kokushi Musou";
            return res;
        }

        if (context.IsMenzen && CheckChiitoitsu(tiles))
        {
            ScoringResult chiitoiRes = new ScoringResult();
            CalculateChiitoiNormal(tiles, context, chiitoiRes);
            CalculatePoints(chiitoiRes, context.IsDealer);
            candidates.Add(chiitoiRes);
        }

        int meldCount = (melds != null) ? melds.Count / 4 : 0;
        var allStructures = DecomposeHandAllPatterns(tiles, meldCount);

        if (allStructures.Count == 0 && candidates.Count == 0)
        {
            ScoringResult err = new ScoringResult();
            err.YakuList.Add("形式エラー(No Mentsu)");
            return err;
        }

        foreach (var structure in allStructures)
        {
            ScoringResult normalRes = new ScoringResult();
            CalculateNormalYaku(structure, tiles, melds, context, normalRes);
            CalculatePoints(normalRes, context.IsDealer);
            candidates.Add(normalRes);
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

    // ==========================================================================================
    //  3. 一般手の役判定ロジック
    // ==========================================================================================

    private static void CalculateChiitoiNormal(int[] tiles, ScoringContext context, ScoringResult result)
    {
        int han = 2;
        List<string> yakus = new List<string> { "ChiToitsu" };
        StringBuilder sb = new StringBuilder();

        AddCommonYaku(yakus, ref han, context, null);
        if (CheckTanyao(tiles, null)) { han++; yakus.Add("Tanyao"); }

        bool isHonroutou = true;
        for (int i = 0; i < 34; i++) { if (tiles[i] == 2 && !IsYaochu(i)) isHonroutou = false; }
        if (isHonroutou) { han += 2; yakus.Add("Hon Roto"); }

        if (CheckChinitsu(tiles, null)) { han += 6; yakus.Add("Chin Itsu"); }
        else if (CheckHonitsu(tiles, null)) { han += 3; yakus.Add("Hon Itsu"); }

        int dora = CountTotalDora(tiles, null, context, yakus, sb);
        han += dora;

        result.Han = han;
        result.Fu = 25; 
        result.YakuList = yakus;
        result.DebugInfo = "Chiitoitsu " + sb.ToString();
    }

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
            if (winId >= startId && winId <= startId + 2)
            {
                if (winId == startId)
                {
                    if (startId % 9 != 0) isRyanmen = true;
                }
                else if (winId == startId + 2)
                {
                    if (startId % 9 != 6) isRyanmen = true;
                }
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

            if (empty)
            {
                results.Add(new List<int>(currentStructure));
            }
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

        if (currentIdx < 27 && currentIdx % 9 < 7 &&
            tiles[currentIdx] > 0 && tiles[currentIdx + 1] > 0 && tiles[currentIdx + 2] > 0)
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
        if (omote > 0) { totalDora += omote; yakuList.Add($"Dora {omote}"); }

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
            if (ura > 0) { totalDora += ura; yakuList.Add($"UraDora {ura}"); }
        }

        if (ctx.RedDoraCount > 0) { totalDora += ctx.RedDoraCount; yakuList.Add($"RedDora {ctx.RedDoraCount}"); }
        if (ctx.NukiDoraCount > 0) { totalDora += ctx.NukiDoraCount; yakuList.Add($"NukiDora {ctx.NukiDoraCount}"); }

        return totalDora;
    }

    private static int CalculateFu(List<int> structure, int[] tiles, List<int> melds, ScoringContext ctx)
    {
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

    private static void CalculatePoints(ScoringResult res, bool isDealer)
    {
        if (res.Han == 0) return;
        if (res.Fu < 20) res.Fu = 20;
        int basicPoints = res.Fu * (int)Math.Pow(2, 2 + res.Han);
        if (basicPoints > 2000 || res.Han >= 5)
        {
            if (res.Han >= 13) { basicPoints = 8000; res.ScoreName = "Yakuman"; }
            else if (res.Han >= 11) { basicPoints = 6000; res.ScoreName = "SanBaiman"; }
            else if (res.Han >= 8) { basicPoints = 4000; res.ScoreName = "Baiman"; }
            else if (res.Han >= 6) { basicPoints = 3000; res.ScoreName = "Haneman"; }
            else { basicPoints = 2000; res.ScoreName = "Mangan"; }
        }

        if (isDealer)
        {
            res.PayRon = RoundUp100(basicPoints * 6);
            int payAll = RoundUp100(basicPoints * 2);
            res.PayTsumoChild = payAll;
            res.PayTsumoDealer = 0;
            res.TotalScore = (res.PayRon > 0) ? res.PayRon : payAll * 3;
        }
        else
        {
            res.PayRon = RoundUp100(basicPoints * 4);
            res.PayTsumoDealer = RoundUp100(basicPoints * 2);
            res.PayTsumoChild = RoundUp100(basicPoints * 1);
            res.TotalScore = (res.PayRon > 0) ? res.PayRon : (res.PayTsumoDealer + res.PayTsumoChild * 2);
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
    private static bool IsKokushi13Wait(int[] t, int w) { if (t[w] < 1) return false; t[w]--; int[] y = { 0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33 }; bool ok = true; foreach (int i in y) if (t[i] != 1) ok = false; t[w]++; return ok; }

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
    private static bool CheckTenhouChiihou(List<string> l, ref int h, ScoringContext c) { if (c.IsFirstTurn && c.IsTsumo) { h = 13; l.Add(c.IsDealer ? "Tenho" : "Chiho"); return true; } return false; }
    private static bool CheckDaisangen(int[] t, List<int> m) { return HasTriplet(t, m, 31) && HasTriplet(t, m, 32) && HasTriplet(t, m, 33); }
    private static bool CheckTsuuissou(int[] t, List<int> m) { for (int i = 0; i < 27; i++) if (t[i] > 0) return false; return true; }
    private static bool CheckRyuuiisou(int[] t, List<int> m) { int[] g = { 19, 20, 21, 23, 25, 32 }; for (int i = 0; i < 34; i++) { if (t[i] > 0 && !Array.Exists(g, x => x == i)) return false; } if (m != null) foreach (int x in m) if (!Array.Exists(g, v => v == x)) return false; return true; }
    private static bool CheckChinroutou(List<int> s, List<int> m) { return CheckHonroutou(s, m) && !IsHonor(s[0]); }
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

    private static void AddCommonYaku(List<string> l, ref int h, ScoringContext c, List<int> s) { if (c.IsDoubleRiichi) { h += 2; l.Add("Double Riichi"); } else if (c.IsRiichi) { h++; l.Add("Riichi"); } if (c.IsIppatsu) { h++; l.Add("Ippatsu"); } if (c.IsRinshan) { h++; l.Add("Rinshan"); } if (c.IsTsumo && c.IsHaitei) { h++; l.Add("Haitei"); } if (!c.IsTsumo && c.IsHoutei) { h++; l.Add("Hotei"); } if (s != null && c.IsMenzen && c.IsTsumo) { h++; l.Add("MenzenTsumo"); } if (c.IsChankan) { h++; l.Add("ChanKan"); } }
    private static bool CheckTanyao(int[] t, List<int> m) { int[] y = { 0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33 }; for (int i = 0; i < 34; i++) if (t[i] > 0 && IsYaochu(i)) return false; if (m != null) foreach (int id in m) if (IsYaochu(id)) return false; return true; }
    private static bool CheckIipeikou(List<int> s) { var l = new List<int>(); for (int i = 1; i < s.Count; i++) if (s[i] < 1000) l.Add(s[i]); return l.GroupBy(x => x).Any(g => g.Count() >= 2); }
    private static int CountIipeikou(List<int> s) { var l = new List<int>(); for (int i = 1; i < s.Count; i++) if (s[i] < 1000) l.Add(s[i]); return l.GroupBy(x => x).Sum(g => g.Count() / 2); }
    private static int CheckYakuhai(int[] t, List<int> m, ScoringContext c, List<string> l) { int h = 0; if (HasTriplet(t, m, 31)) { h++; l.Add("役牌 Haku"); } if (HasTriplet(t, m, 32)) { h++; l.Add("役牌 Hatsu"); } if (HasTriplet(t, m, 33)) { h++; l.Add("役牌 Chun"); } int rw = 27 + c.RoundWind; if (HasTriplet(t, m, rw)) { h++; l.Add("場風牌 Double Ton"); } int sw = 27 + c.SeatWind; if (HasTriplet(t, m, sw)) { h++; l.Add("自風牌 My Wind"); } return h; }
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
}
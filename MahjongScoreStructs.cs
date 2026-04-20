using System.Collections.Generic;

public class ScoringResult
{
    public int Han;            
    public int Fu;             
    public int TotalScore;     
    
    public int PayRon;         
    public int PayTsumoDealer; 
    public int PayTsumoChild;  
    
    public List<string> YakuList = new List<string>();
    public string ScoreName;

    public string DebugInfo;
}

public class ScoringContext
{
    public bool IsTsumo;       
    public bool IsMenzen;      
    public bool IsRiichi;      
    public bool IsDoubleRiichi;
    public bool IsIppatsu;     
    public bool IsRinshan;     
    public bool IsChankan;     
    public bool IsHaitei;      
    public bool IsHoutei;      

    public bool IsFirstTurn;
    public bool IsDealer;
    
    public int SeatWind;       
    public int RoundWind;      
    
    // --- ドラ関連 ---
    public List<int> DoraTiles;      
    public List<int> UraDoraTiles;   
    public int RedDoraCount;         
    public int NukiDoraCount;        
    
    public int WinningTileId; 
    public int HonbaCount;

    // ★追加: 和了時に有効だった特別役トリガーのリスト
    public List<string> SpecialYakuTriggers = new List<string>();
}
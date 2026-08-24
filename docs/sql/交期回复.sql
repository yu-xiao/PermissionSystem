
/****** 对象:  StoredProcedure [dbo].[P_FZPCJQHFB_NEW]    脚本日期: 2026/8/19 17:01:27 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
    ALTER proc [dbo].[P_FZPCJQHFB_NEW] (@Year INT,
    @Month INT)
    AS
    BEGIN

    SET NOCOUNT ON;

    DECLARE @StartDate date = TRY_CONVERT(
        date,
        CONCAT(@Year, RIGHT('0' + CONVERT(varchar(2), @Month), 2), '01'),
        112
    );
    DECLARE @EndDate date = CASE
        WHEN @StartDate IS NULL THEN NULL
        WHEN @StartDate = CONVERT(date, '99991201', 112) THEN NULL
        ELSE DATEADD(MONTH, 1, @StartDate)
    END;
    
    SELECT 
        交期 = a.F_JN_CUSTOMERDELIVERDATE,
        单据类型 = e.FNAME,
        半成品名称 = d.fname,
        系列 = ISNULL(xl.fname, ''),
        工艺 = ISNULL(gy.fname, ''),
        颜色 = ISNULL(ys.fname, ''),
        --工件名称 = ISNULL(c.F_JNXM_WORKPIECENAME, ''),
        订单ID = a.fid,
        子单号 = c.f_jnxm_partsascno,
        订单号=b.f_jn_mtono
    FROM JN_T_Sal_IntentOrder a
    JOIN JN_t_SAL_TicketDetails b ON a.fid = b.fid
    JOIN JNXM_t_IntentSubEntity c ON b.fentryid = c.fentryid
    JOIN t_bd_material_l d ON c.f_jnxm_partid = d.fmaterialid
    JOIN T_BAS_BILLTYPE_L e ON a.FBILLTYPEID = e.FBILLTYPEID
    LEFT JOIN T_BD_FLEXSITEMDETAILV sx ON c.F_JNXM_PARTAUXPROPID = sx.fid
    LEFT JOIN JN_T_BD_MaterialSeries_l xl ON sx.FF100011 = xl.fid
    LEFT JOIN JNXM_t_Craft_L gy ON sx.ff100014 = gy.fid
    LEFT JOIN jnxm_t_color_l ys ON sx.ff100013 = ys.fid
    WHERE @StartDate IS NOT NULL
      AND a.F_JN_CUSTOMERDELIVERDATE >= @StartDate
      AND (@EndDate IS NULL OR a.F_JN_CUSTOMERDELIVERDATE < @EndDate);

      select c.FDATAVALUE,F_JNXM_SERIALKEY 系列条件 ,F_JNXM_CRAFTKEY 工艺条件,F_JNXM_COLORKEY 颜色条件,F_JNXM_PARTKEY 半成品条件
,case when len(F_JNXM_SERIALKEY) = 0 then 0 else 1 end  + case when len(F_JNXM_CRAFTKEY) = 0 then 0 else 1 end
+case when len(F_JNXM_COLORKEY) = 0 then 0 else 1 end + case when len(F_JNXM_PARTKEY) = 0 then 0 else 1 end num
into #temp_a -- drop table #temp_a
from T_JN_LeadTimeEntry a
join T_JN_LeadTimeEntry_Ser b on a.FEntryID = b.FEntryID
join T_BAS_ASSISTANTDATAENTRY_L c on b.F_JNXM_SERICATEGORYS = c.FENTRYID

---select * from #temp_a
--select * from #tempordinfo

--select * from #temp_a

-- ==========================================
-- 1. 声明变量
-- ==========================================
DECLARE @sql NVARCHAR(MAX) = N'';
DECLARE @case_when NVARCHAR(MAX) = N'';
DECLARE @fdatavalue NVARCHAR(100);
DECLARE @series_cond NVARCHAR(MAX); -- 读取的系列条件
DECLARE @process_cond NVARCHAR(MAX); -- 读取的工艺条件
DECLARE @ys_cond NVARCHAR(MAX); -- 读取的颜色条件
DECLARE @cp_cond NVARCHAR(MAX); -- 读取的半成品条件

-- ==========================================
-- 2. 定义游标，逐行读取 #temp_a 表中的规则
-- ==========================================
DECLARE cur_rules CURSOR LOCAL FAST_FORWARD FOR
SELECT FDATAVALUE, [系列条件], [工艺条件] ,颜色条件,半成品条件 
FROM #temp_a  order by num desc;

OPEN cur_rules;

FETCH NEXT FROM cur_rules INTO @fdatavalue, @series_cond, @process_cond,@ys_cond,@cp_cond;

WHILE @@FETCH_STATUS = 0
BEGIN

    SET @series_cond = REPLACE(@series_cond, 'FName', 'a.系列');
    SET @process_cond = REPLACE(@process_cond, 'FName', 'a.工艺');
    SET @ys_cond = REPLACE(@ys_cond, 'FName', 'a.颜色');
    SET @cp_cond = REPLACE(@cp_cond, 'FName', 'a.半成品名称');

     DECLARE @current_when NVARCHAR(MAX) = N' WHEN ';
    DECLARE @is_first BIT = 1; 

    -- 1. 拼系列
    IF (@series_cond IS NOT NULL AND @series_cond != '')
    BEGIN
        IF (@is_first = 0) SET @current_when = @current_when + N' AND ';
        SET @current_when = @current_when + N'(' + @series_cond + N')';
        SET @is_first = 0;
    END

    -- 2. 拼工艺
    IF (@process_cond IS NOT NULL AND @process_cond != '')
    BEGIN
        IF (@is_first = 0) SET @current_when = @current_when + N' AND ';
        SET @current_when = @current_when + N'(' + @process_cond + N')';
        SET @is_first = 0;
    END

    -- 3. 拼颜色
    IF (@ys_cond IS NOT NULL AND @ys_cond != '')
    BEGIN
        IF (@is_first = 0) SET @current_when = @current_when + N' AND ';
        SET @current_when = @current_when + N'(' + @ys_cond + N')';
        SET @is_first = 0;
    END

    -- 4. 拼半成品
    IF (@cp_cond IS NOT NULL AND @cp_cond != '')
    BEGIN
        IF (@is_first = 0) SET @current_when = @current_when + N' AND ';
        SET @current_when = @current_when + N'(' + @cp_cond + N')';
        SET @is_first = 0;
    END

    IF (@is_first = 1) 
    BEGIN
        SET @current_when = @current_when + N'(1=1)'; 
    END


    SET @current_when = @current_when + N' THEN N' + QUOTENAME(@fdatavalue, '''') + N' ' + CHAR(13);

    SET @case_when = CONVERT(NVARCHAR(MAX), @case_when) + @current_when;


    FETCH NEXT FROM cur_rules INTO @fdatavalue, @series_cond, @process_cond,@ys_cond,@cp_cond;
END

CLOSE cur_rules;
DEALLOCATE cur_rules;

SET @sql = N'
SELECT 
    a.交期, a.单据类型, a.半成品名称, a.子单号, a.系列, a.工艺, a.颜色, a.订单ID, a.订单号,
    CASE ' + @case_when + N' 
        ELSE N''未归类'' 
    END AS FDATAVALUE
FROM #tempordinfo a
';

create table #tempordresult(
交期 date,
单据类型 nvarchar(50),
半成品名称 nvarchar(50),
子单号 nvarchar(50),
系列 nvarchar(50),
工艺 nvarchar(50),
颜色 nvarchar(50),
订单id nvarchar(50),
订单号 nvarchar(50),
datavalue nvarchar(50)
)
--select @sql;

DECLARE @stmt NVARCHAR(MAX) = @sql;


insert into #tempordresult
EXEC sp_executesql @stmt = @stmt;


---获取产能
   SELECT 
        单据类型 = c.FNAME,
        系列大类 = d.FDATAVALUE,
        产能 = MAX(a.FSTANDARDCAPACITY)
    INTO #tempbjcnts
    FROM JN_t_WorkCapacityEntry a
    JOIN JN_t_WorkCapacity b ON a.fid = b.fid
    JOIN T_BAS_BILLTYPE_L c ON b.FBILLTYPEid = c.FBILLTYPEid
    JOIN T_BAS_ASSISTANTDATAENTRY_l d ON a.F_JNXM_SERICATEGORY = d.fentryid
    WHERE b.FUSEDORG = '100079'
    GROUP BY c.FNAME, d.FDATAVALUE;

 WITH DailyDetail AS (
        SELECT 
            DAY(f.交期) AS 日,
            f.datavalue,
            f.单据类型,
            f.订单ID,
            CASE WHEN f.datavalue LIKE '%外购%' OR f.datavalue LIKE '%五金%' OR f.datavalue LIKE '%线材%' OR f.datavalue LIKE '%方管%' 
                 THEN 1 ELSE 0 END AS is_special
        FROM #tempordresult f
        WHERE f.datavalue <> '未分类'
    ),
    DailyCount AS (
        SELECT 
            日,
            datavalue,
            单据类型,
            SUM(CASE WHEN is_special = 1 THEN 1 ELSE 0 END) 
            + COUNT(DISTINCT CASE WHEN is_special = 0 THEN 订单ID ELSE NULL END) AS 订单数
        FROM DailyDetail
        GROUP BY 日, datavalue, 单据类型
    )
    SELECT 
        @Year AS 年份,
        @Month AS 月份,
        dc.datavalue,
        dc.单据类型,
        ISNULL(MAX(c.产能), 0) AS 产能,
        ISNULL(SUM(CASE WHEN dc.日 = 1  THEN dc.订单数 ELSE 0 END), 0) AS [1],
        ISNULL(SUM(CASE WHEN dc.日 = 2  THEN dc.订单数 ELSE 0 END), 0) AS [2],
        ISNULL(SUM(CASE WHEN dc.日 = 3  THEN dc.订单数 ELSE 0 END), 0) AS [3],
        ISNULL(SUM(CASE WHEN dc.日 = 4  THEN dc.订单数 ELSE 0 END), 0) AS [4],
        ISNULL(SUM(CASE WHEN dc.日 = 5  THEN dc.订单数 ELSE 0 END), 0) AS [5],
        ISNULL(SUM(CASE WHEN dc.日 = 6  THEN dc.订单数 ELSE 0 END), 0) AS [6],
        ISNULL(SUM(CASE WHEN dc.日 = 7  THEN dc.订单数 ELSE 0 END), 0) AS [7],
        ISNULL(SUM(CASE WHEN dc.日 = 8  THEN dc.订单数 ELSE 0 END), 0) AS [8],
        ISNULL(SUM(CASE WHEN dc.日 = 9  THEN dc.订单数 ELSE 0 END), 0) AS [9],
        ISNULL(SUM(CASE WHEN dc.日 = 10  THEN dc.订单数 ELSE 0 END), 0) AS [10],
        ISNULL(SUM(CASE WHEN dc.日 = 11  THEN dc.订单数 ELSE 0 END), 0) AS [11],
        ISNULL(SUM(CASE WHEN dc.日 = 12 THEN dc.订单数 ELSE 0 END), 0) AS [12],
        ISNULL(SUM(CASE WHEN dc.日 = 13  THEN dc.订单数 ELSE 0 END), 0) AS [13],
        ISNULL(SUM(CASE WHEN dc.日 = 14  THEN dc.订单数 ELSE 0 END), 0) AS [14],
        ISNULL(SUM(CASE WHEN dc.日 = 15  THEN dc.订单数 ELSE 0 END), 0) AS [15],
        ISNULL(SUM(CASE WHEN dc.日 = 16 THEN dc.订单数 ELSE 0 END), 0) AS [16],
        ISNULL(SUM(CASE WHEN dc.日 = 17  THEN dc.订单数 ELSE 0 END), 0) AS [17],
        ISNULL(SUM(CASE WHEN dc.日 = 18  THEN dc.订单数 ELSE 0 END), 0) AS [18],
        ISNULL(SUM(CASE WHEN dc.日 = 19  THEN dc.订单数 ELSE 0 END), 0) AS [19],
        ISNULL(SUM(CASE WHEN dc.日 = 20  THEN dc.订单数 ELSE 0 END), 0) AS [20],
        ISNULL(SUM(CASE WHEN dc.日 = 21  THEN dc.订单数 ELSE 0 END), 0) AS [21],
        ISNULL(SUM(CASE WHEN dc.日 = 22  THEN dc.订单数 ELSE 0 END), 0) AS [22],
        ISNULL(SUM(CASE WHEN dc.日 = 23  THEN dc.订单数 ELSE 0 END), 0) AS [23],
        ISNULL(SUM(CASE WHEN dc.日 = 24  THEN dc.订单数 ELSE 0 END), 0) AS [24],
        ISNULL(SUM(CASE WHEN dc.日 = 25  THEN dc.订单数 ELSE 0 END), 0) AS [25],
        ISNULL(SUM(CASE WHEN dc.日 = 26  THEN dc.订单数 ELSE 0 END), 0) AS [26],
        ISNULL(SUM(CASE WHEN dc.日 = 27  THEN dc.订单数 ELSE 0 END), 0) AS [27],
        ISNULL(SUM(CASE WHEN dc.日 = 28  THEN dc.订单数 ELSE 0 END), 0) AS [28],
        ISNULL(SUM(CASE WHEN dc.日 = 29  THEN dc.订单数 ELSE 0 END), 0) AS [29],
        ISNULL(SUM(CASE WHEN dc.日 = 30  THEN dc.订单数 ELSE 0 END), 0) AS [30],
        ISNULL(SUM(CASE WHEN dc.日 = 31  THEN dc.订单数 ELSE 0 END), 0) AS [31]

    FROM DailyCount dc
    LEFT JOIN #tempbjcnts c ON dc.单据类型 = c.单据类型 AND dc.datavalue = c.系列大类
    GROUP BY dc.datavalue, dc.单据类型
    ORDER BY dc.datavalue, dc.单据类型;

END

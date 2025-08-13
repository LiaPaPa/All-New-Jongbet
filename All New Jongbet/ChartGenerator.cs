using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
// OxyPlot 네임스페이스 사용
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;

namespace All_New_Jongbet
{
    public class ChartGenerator
    {
        public string CreateAssetTrendChartImage(List<DailyAssetInfo> assetData, string title)
        {
            if (assetData == null || assetData.Count < 2)
            {
                return null;
            }

            // 1. PlotModel 생성 및 테마 설정
            var model = new PlotModel
            {
                Title = title,
                TitleColor = OxyColors.Black,
                PlotAreaBorderColor = OxyColors.LightGray,
                Background = OxyColors.White,
                TextColor = OxyColors.Black,
                Padding = new OxyThickness(10, 10, 20, 10)
            };

            // 2. X축 (날짜) 설정
            var dateAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "MM-dd",
                MajorGridlineStyle = LineStyle.None, // X축 그리드 라인 제거
                MinorGridlineStyle = LineStyle.None,
                AxislineColor = OxyColors.Black,
                TicklineColor = OxyColors.Black,
                TextColor = OxyColors.Black,
                Title = "Date",
            };
            model.Axes.Add(dateAxis);

            // [CHANGED] Y축 범위 자동 계산 로직 추가
            double minValue = assetData.Min(d => d.EstimatedAsset);
            double maxValue = assetData.Max(d => d.EstimatedAsset);
            double padding = (maxValue - minValue) * 0.1; // 상하 10% 여백

            // 3. Y축 (자산) 설정
            var valueAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                MajorGridlineStyle = LineStyle.Solid, // Y축 그리드 라인 유지
                MajorGridlineColor = OxyColor.FromRgb(230, 230, 230),
                MinorGridlineStyle = LineStyle.None,
                AxislineColor = OxyColors.Black,
                TicklineColor = OxyColors.Black,
                TextColor = OxyColors.Black,
                StringFormat = "N0",
                Title = "Assets (KRW)",
                // Y축의 최소/최대값을 데이터에 맞춰 동적으로 설정
                Minimum = minValue - padding,
                Maximum = maxValue + padding,
            };
            model.Axes.Add(valueAxis);

            // 4. 데이터 시리즈 생성 (음영 효과 포함)
            var areaSeries = new AreaSeries
            {
                StrokeThickness = 2,
                Color = OxyColor.FromRgb(250, 200, 50), // System Blue
                Fill = OxyColor.FromArgb(60, 250, 200, 50), // 반투명 System Blue
            };

            foreach (var dataPoint in assetData)
            {
                areaSeries.Points.Add(DateTimeAxis.CreateDataPoint(dataPoint.DateObject, dataPoint.EstimatedAsset));
            }
            model.Series.Add(areaSeries);

            // 5. 이미지 파일로 내보내기
            string tempPath = Path.Combine(Path.GetTempPath(), $"asset_trend_{DateTime.Now.Ticks}_{title.Replace(" ", "_")}.png");
            var pngExporter = new PngExporter { Width = 800, Height = 400 };
            pngExporter.ExportToFile(model, tempPath);

            return tempPath;
        }
    }
}
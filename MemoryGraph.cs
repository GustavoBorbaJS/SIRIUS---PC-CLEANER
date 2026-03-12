using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;

namespace ProcessCleaner
{
    public class MemoryGraph : UserControl
    {
        private List<float> memoryHistory = new List<float>();
        private System.Windows.Forms.Timer refreshTimer; // Especificado completamente
        private PerformanceCounter ramCounter;
        private const int MaxPoints = 60; // 60 pontos = 60 segundos
        private Color graphColor = Color.FromArgb(52, 152, 219);
        private Color gridColor = Color.FromArgb(200, 200, 200);

        public MemoryGraph()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(240, 240, 245);
            this.Size = new Size(280, 200);
            
            try
            {
                ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao criar PerformanceCounter: {ex.Message}");
            }

            refreshTimer = new System.Windows.Forms.Timer(); // Especificado completamente
            refreshTimer.Interval = 1000;
            refreshTimer.Tick += (s, e) => UpdateMemoryData();
            refreshTimer.Start();
        }

        private void UpdateMemoryData()
        {
            try
            {
                if (ramCounter == null) return;
                
                float availableMemory = ramCounter.NextValue();
                float totalMemory = GetTotalMemory();
                
                if (totalMemory <= 0) return;
                
                float usedMemory = totalMemory - availableMemory;
                float usagePercent = (usedMemory / totalMemory) * 100;

                // Garantir que o percentual está entre 0 e 100
                usagePercent = Math.Max(0, Math.Min(100, usagePercent));

                memoryHistory.Add(usagePercent);
                
                // Mantém apenas os últimos MaxPoints
                if (memoryHistory.Count > MaxPoints)
                    memoryHistory.RemoveAt(0);

                this.Invalidate(); // Redesenha
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao atualizar gráfico: {ex.Message}");
            }
        }

        private float GetTotalMemory()
        {
            try
            {
                // Método alternativo para obter memória total
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"))
                {
                    foreach (System.Management.ManagementObject obj in searcher.Get())
                    {
                        return Convert.ToSingle(obj["TotalVisibleMemorySize"]) / 1024; // Converte de KB para MB
                    }
                }
                return 16384; // 16GB como fallback
            }
            catch
            {
                // Fallback: estimativa baseada em memória disponível + uso atual
                try
                {
                    if (ramCounter != null)
                    {
                        float available = ramCounter.NextValue();
                        return available * 2; // Estimativa grosseira
                    }
                }
                catch { }
                
                return 16384; // 16GB como fallback
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int width = this.Width;
            int height = this.Height;

            // Desenha fundo
            g.Clear(this.BackColor);

            // Desenha borda
            using (Pen borderPen = new Pen(Color.FromArgb(200, 200, 200)))
            {
                g.DrawRectangle(borderPen, 1, 1, width - 3, height - 3);
            }

            // Desenha linhas de grade horizontais (0%, 25%, 50%, 75%, 100%)
            using (Pen gridPen = new Pen(gridColor) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
            {
                for (int i = 0; i <= 4; i++)
                {
                    float y = 25 + ((height - 50) * (4 - i) / 4f);
                    g.DrawLine(gridPen, 40, y, width - 15, y);
                }
            }

            // Desenha eixos
            using (Pen axisPen = new Pen(Color.FromArgb(100, 100, 100), 1))
            {
                g.DrawLine(axisPen, 40, 20, 40, height - 25); // Eixo Y
                g.DrawLine(axisPen, 40, height - 25, width - 15, height - 25); // Eixo X
            }

            // Desenha rótulos do eixo Y
            using (Font labelFont = new Font("Segoe UI", 7))
            using (Brush labelBrush = new SolidBrush(Color.FromArgb(80, 80, 80)))
            {
                for (int i = 0; i <= 4; i++)
                {
                    float y = 20 + ((height - 50) * (4 - i) / 4f);
                    g.DrawString($"{i * 25}%", labelFont, labelBrush, 5, y - 7);
                }
            }

            // Desenha o gráfico (velas/barras)
            if (memoryHistory.Count > 1)
            {
                float barWidth = (width - 70) / (float)MaxPoints;
                float startX = 45;

                using (Pen barPen = new Pen(Color.FromArgb(50, 50, 100), 1))
                {
                    for (int i = 0; i < memoryHistory.Count; i++)
                    {
                        float value = memoryHistory[i];
                        float barHeight = (value / 100f) * (height - 60);
                        float x = startX + (i * barWidth);
                        float y = height - 30 - barHeight;

                        // Cor baseada na intensidade do uso
                        Color barColor = value > 80 ? Color.FromArgb(231, 76, 60) :   // Vermelho
                                       value > 60 ? Color.FromArgb(241, 196, 15) :   // Amarelo
                                       value > 40 ? Color.FromArgb(46, 204, 113) :   // Verde
                                       graphColor;                                    // Azul
                        
                        using (Brush colorBrush = new SolidBrush(barColor))
                        {
                            g.FillRectangle(colorBrush, x, y, barWidth - 2, barHeight);
                        }
                        g.DrawRectangle(barPen, x, y, barWidth - 2, barHeight);
                    }
                }
            }

            // Desenha título
            using (Font titleFont = new Font("Segoe UI", 9, FontStyle.Bold))
            using (Brush titleBrush = new SolidBrush(Color.FromArgb(52, 73, 94)))
            {
                g.DrawString("Monitor de RAM", titleFont, titleBrush, 45, 5);
            }

            // Mostra valor atual
            if (memoryHistory.Count > 0)
            {
                float currentUsage = memoryHistory[memoryHistory.Count - 1];
                using (Font valueFont = new Font("Segoe UI", 8, FontStyle.Bold))
                using (Brush valueBrush = new SolidBrush(graphColor))
                {
                    string currentText = $"Agora: {currentUsage:F1}%";
                    g.DrawString(currentText, valueFont, valueBrush, width - 100, 8);
                }
            }

            // Mostra legenda
            using (Font legendFont = new Font("Segoe UI", 6))
            using (Brush legendBrush = new SolidBrush(Color.FromArgb(120, 120, 120)))
            {
                g.DrawString("Últimos 60 segundos", legendFont, legendBrush, 45, height - 18);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                refreshTimer?.Stop();
                refreshTimer?.Dispose();
                ramCounter?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
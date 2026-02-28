import re

with open("src/Afney.Cad.Presentation/MainWindow.xaml", "r", encoding="utf-8") as f:
    text = f.read()

# We want to replace everything from <!-- ═══════ TAB 1: HOME (AutoCAD Style) ═══════ -->
# to </TabControl>
start_marker = "<!-- ═══════ TAB 1: HOME (AutoCAD Style) ═══════ -->"
end_marker = "</TabControl>"

start_idx = text.find(start_marker)
end_idx = text.find(end_marker)

if start_idx == -1 or end_idx == -1:
    print("Markers not found!")
    exit(1)

new_tabs = """<!-- ═══════ TAB 1: SİSTEM (System Definition) ═══════ -->
                    <TabItem Header="1. Sistem" x:Name="TabSystem">
                        <Border Background="#333337" Padding="4,4,4,2">
                            <WrapPanel>
                                <Border BorderBrush="#FF9800" BorderThickness="0,0,0,2" Margin="0,0,8,0" Padding="4,2">
                                    <StackPanel>
                                        <Button Content="⚙️ Norm Seçimi" Click="OnStandardSelection" Style="{StaticResource RbnAccent}" ToolTip="Hesap standardı"/>
                                        <TextBlock Text="Standart" Foreground="#666" FontSize="9" HorizontalAlignment="Center" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                                <Border BorderBrush="#FFD700" BorderThickness="0,0,0,2" Margin="0,0,8,0" Padding="4,2">
                                    <StackPanel>
                                        <StackPanel Orientation="Horizontal">
                                            <Button Content="🏠 Özellikler" Click="OnBuildingProperties" Style="{StaticResource RbnBtn}"/>
                                            <Button Content="📶 Kat Yönetici" Click="OnLevelManager" Style="{StaticResource RbnBtn}"/>
                                            <Button Content="🏢 Çok Katlı" Click="OnMultiStoryManager" Style="{StaticResource RbnAccent}"/>
                                        </StackPanel>
                                        <TextBlock Text="Bina Ayarları" Foreground="#666" FontSize="9" HorizontalAlignment="Center" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                                <Border BorderBrush="#444" BorderThickness="0,0,1,0" Margin="0,0,4,0" Padding="4,2,8,2">
                                    <StackPanel>
                                        <StackPanel Orientation="Vertical" Height="40" VerticalAlignment="Center" Margin="4,0">
                                            <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
                                                <TextBlock Text="System:" Foreground="#AAA" Width="45" VerticalAlignment="Center" FontSize="10"/>
                                                <ComboBox x:Name="SystemTypeCombo" Width="90" SelectedIndex="0" SelectionChanged="OnMechanicalSettingsChanged" FontSize="10" Height="18" Padding="2,0">
                                                    <ComboBoxItem Content="Waste Water" Tag="WasteWater"/>
                                                    <ComboBoxItem Content="Cold Water" Tag="DomesticColdWater"/>
                                                    <ComboBoxItem Content="Hot Water" Tag="DomesticHotWater"/>
                                                </ComboBox>
                                            </StackPanel>
                                            <StackPanel Orientation="Horizontal">
                                                <TextBlock Text="Size/Mat:" Foreground="#AAA" Width="45" VerticalAlignment="Center" FontSize="10"/>
                                                <ComboBox x:Name="PipeSizeCombo" Width="45" SelectedIndex="2" SelectionChanged="OnMechanicalSettingsChanged" FontSize="10" Height="18" Padding="2,0" Margin="0,0,2,0">
                                                    <ComboBoxItem Content="50" Tag="50"/><ComboBoxItem Content="75" Tag="75"/>
                                                    <ComboBoxItem Content="110" Tag="110"/><ComboBoxItem Content="160" Tag="160"/>
                                                </ComboBox>
                                                <ComboBox x:Name="MaterialCombo" Width="43" SelectedIndex="0" SelectionChanged="OnMechanicalSettingsChanged" FontSize="10" Height="18" Padding="2,0">
                                                    <ComboBoxItem Content="PVC" Tag="PVC"/><ComboBoxItem Content="PP-R" Tag="PPR"/>
                                                </ComboBox>
                                            </StackPanel>
                                        </StackPanel>
                                        <TextBlock Text="Boru Varsayılanları" Foreground="#888" FontSize="10" HorizontalAlignment="Center" Margin="0,4,0,0"/>
                                    </StackPanel>
                                </Border>
                                
                                <Border BorderBrush="#007ACC" BorderThickness="0,0,0,2" Margin="0,0,8,0" Padding="4,2">
                                    <StackPanel>
                                        <Button Content="✅ Ayarları Onayla" Click="OnConfirmSystemSettings" Style="{StaticResource RbnOk}" ToolTip="Sonraki sekmelerin kilidini açar"/>
                                        <TextBlock Text="Süreç" Foreground="#666" FontSize="9" HorizontalAlignment="Center" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                            </WrapPanel>
                        </Border>
                    </TabItem>

                    <!-- ═══════ TAB 2: UÇ NOKTALAR (Terminals) ═══════ -->
                    <TabItem Header="2. Uç Noktalar" x:Name="TabTerminals" IsEnabled="False">
                        <Border Background="#333337" Padding="4,4,4,2">
                            <WrapPanel>
                                <Border BorderBrush="#444" BorderThickness="0,0,1,0" Margin="0,0,4,0" Padding="4,2,8,2">
                                    <StackPanel>
                                        <StackPanel Orientation="Horizontal" Height="40">
                                            <Button Click="OnFixtureLibrary" Style="{StaticResource RbnBtn}" ToolTip="Reseptör Kütüphanesi" Width="45">
                                                <StackPanel>
                                                    <Path Data="M 4 4 H 20 V 20 H 4 Z M 4 8 H 20 M 12 8 V 20" Stroke="#DDD" StrokeThickness="1" HorizontalAlignment="Center" Margin="0,2"/>
                                                    <TextBlock Text="Kütüphane" FontSize="9"/>
                                                </StackPanel>
                                            </Button>
                                            <Button Click="OnPlaceFixtureOnWall" Style="{StaticResource RbnBtn}" ToolTip="Duvara Cihaz Yerleştir" Margin="2,0" Width="45">
                                                <StackPanel>
                                                    <Path Data="M 12 2 A 4 4 0 0 0 8 6 V 12 H 16 V 6 A 4 4 0 0 0 12 2 Z M 6 12 H 18 V 16 H 6 Z M 12 16 V 22" Stroke="#DDD" StrokeThickness="1" Fill="#555" HorizontalAlignment="Center" Margin="0,2"/>
                                                    <TextBlock Text="Cihaz At" FontSize="9"/>
                                                </StackPanel>
                                            </Button>
                                        </StackPanel>
                                        <TextBlock Text="Reseptörler" Foreground="#888" FontSize="10" HorizontalAlignment="Center" Margin="0,4,0,0"/>
                                    </StackPanel>
                                </Border>
                                <Border BorderBrush="#00CC88" BorderThickness="0,0,0,2" Margin="0,0,8,0" Padding="4,2">
                                    <StackPanel>
                                        <StackPanel Orientation="Horizontal">
                                            <Button Content="🏠 Akıllı" Click="OnSmartDetectRoomClick" Style="{StaticResource RbnOk}" ToolTip="Otomatik oda tanıma"/>
                                            <Button Content="📍 Manuel" Click="OnSelectRoom" Style="{StaticResource RbnBtn}"/>
                                            <Button Content="🏷️ Tanımla" Click="OnDefineMahalCommand" Style="{StaticResource RbnBtn}"/>
                                        </StackPanel>
                                        <TextBlock Text="Mahal &amp; Zemin" Foreground="#666" FontSize="9" HorizontalAlignment="Center" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                            </WrapPanel>
                        </Border>
                    </TabItem>

                    <!-- ═══════ TAB 3: TESİSAT (Routing) ═══════ -->
                    <TabItem Header="3. Tesisat" x:Name="TabRouting" IsEnabled="False">
                        <Border Background="#333337" Padding="4,4,4,2">
                            <WrapPanel>
                                <Border BorderBrush="#444" BorderThickness="0,0,1,0" Margin="0,0,4,0" Padding="4,2,8,2">
                                    <StackPanel>
                                        <StackPanel Orientation="Horizontal" Height="40">
                                            <Button Click="OnDrawPipeCommand" Style="{StaticResource RbnAccent}" ToolTip="Boru Çiz (P)" Margin="2,0" Width="40">
                                                <StackPanel>
                                                    <Path Data="M 2 8 L 22 8 M 2 16 L 22 16 M 2 8 V 16 M 22 8 V 16" Stroke="#00DDFF" StrokeThickness="1.5" HorizontalAlignment="Center" Margin="0,2"/>
                                                    <TextBlock Text="Pipe" FontSize="9"/>
                                                </StackPanel>
                                            </Button>
                                            <Button Click="OnWallParallelRoute" Style="{StaticResource RbnBtn}" ToolTip="Duvara Paralel Boru" Margin="2,0" Width="45">
                                                <StackPanel>
                                                    <Path Data="M 4 2 V 22 M 10 2 V 22 M 4 6 H 10 M 4 12 H 10 M 4 18 H 10 M 16 6 V 18 M 14 6 H 18 M 14 18 H 18" Stroke="#DDD" StrokeThickness="1" HorizontalAlignment="Center" Margin="0,2"/>
                                                    <TextBlock Text="Parallel" FontSize="9"/>
                                                </StackPanel>
                                            </Button>
                                        </StackPanel>
                                        <TextBlock Text="Boru Çizimi" Foreground="#888" FontSize="10" HorizontalAlignment="Center" Margin="0,4,0,0"/>
                                    </StackPanel>
                                </Border>
                                <Border BorderBrush="#444" BorderThickness="0,0,1,0" Margin="0,0,4,0" Padding="4,2,8,2">
                                    <StackPanel>
                                        <StackPanel Orientation="Horizontal" Height="40">
                                            <Button Click="OnPipeWizard" Style="{StaticResource RbnBtn}" ToolTip="Boru Makrosu/Sihirbazı" Width="45">
                                                <StackPanel>
                                                    <Path Data="M 4 20 L 16 8 L 20 12 L 8 24 Z M 16 8 L 20 4 M 18 2 L 22 6" Stroke="#FFF" StrokeThickness="1" Fill="#44DD88" HorizontalAlignment="Center" Margin="0,2"/>
                                                    <TextBlock Text="Wizard" FontSize="9"/>
                                                </StackPanel>
                                            </Button>
                                            <Button Click="OnAutoBranchingClick" Style="{StaticResource RbnBtn}" ToolTip="Otomatik Şube Bağlantısı" Width="45">
                                                <StackPanel>
                                                    <Path Data="M 8 12 A 4 4 0 1 1 8 4 A 4 4 0 1 1 8 12 M 16 20 A 4 4 0 1 1 16 12 A 4 4 0 1 1 16 20 M 10 9 L 14 15" Stroke="#AA66FF" StrokeThickness="1" HorizontalAlignment="Center" Margin="0,2"/>
                                                    <TextBlock Text="Auto-Con" FontSize="9"/>
                                                </StackPanel>
                                            </Button>
                                            <Button Click="OnRiserConnection" Style="{StaticResource RbnBtn}" ToolTip="Kolon Bağlantısı" Width="40">
                                                <StackPanel>
                                                    <Path Data="M 8 22 V 2 M 16 22 V 2 M 4 18 H 20 M 4 12 H 20 M 4 6 H 20" Stroke="#DDD" StrokeThickness="1.5" HorizontalAlignment="Center" Margin="0,2"/>
                                                    <TextBlock Text="Riser" FontSize="9"/>
                                                </StackPanel>
                                            </Button>
                                        </StackPanel>
                                        <TextBlock Text="Akıllı Bağlantı" Foreground="#888" FontSize="10" HorizontalAlignment="Center" Margin="0,4,0,0"/>
                                    </StackPanel>
                                </Border>
                                <Border BorderBrush="#FF4444" BorderThickness="0,0,0,2" Margin="0,0,8,0" Padding="4,2">
                                    <StackPanel>
                                        <Button Content="🔥 Sprinkler" Click="OnFireFightingDesign" Style="{StaticResource RbnWarn}"/>
                                        <TextBlock Text="Yangın" Foreground="#666" FontSize="9" HorizontalAlignment="Center" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                                <Border BorderBrush="#87CEEB" BorderThickness="0,0,0,2" Margin="0,0,8,0" Padding="4,2">
                                    <StackPanel>
                                        <StackPanel Orientation="Horizontal">
                                            <Button Content="🚿 Pis Su" Click="OnWasteWaterDesign" Style="{StaticResource RbnBtn}" ToolTip="Pis Su / Yağmur"/>
                                            <Button Content="🧯 Fosseptik" Click="OnSepticTankDesign" Style="{StaticResource RbnBtn}"/>
                                        </StackPanel>
                                        <TextBlock Text="Pis Su Modülleri" Foreground="#666" FontSize="9" HorizontalAlignment="Center" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                                
                                <Border BorderBrush="#444" BorderThickness="0,0,1,0" Margin="0,0,4,0" Padding="4,2,8,2">
                                    <StackPanel>
                                        <StackPanel Orientation="Horizontal" Height="40">
                                            <Button Click="OnLineCommand" Style="{StaticResource RbnBtn}" ToolTip="Çizgi (L)" Width="40">
                                                <StackPanel>
                                                    <Path Data="M 4,20 L 20,4" Stroke="#00AAFF" StrokeThickness="1.5" HorizontalAlignment="Center" Margin="0,2"/>
                                                    <TextBlock Text="Line" FontSize="9"/>
                                                </StackPanel>
                                            </Button>
                                            <Button Click="OnCircleCommand" Style="{StaticResource RbnBtn}" ToolTip="Daire (C)" Width="40">
                                                <StackPanel>
                                                    <Path Data="M 12 4 A 8 8 0 1 1 12 20 A 8 8 0 1 1 12 4 Z" Stroke="#00AAFF" StrokeThickness="1.5" HorizontalAlignment="Center" Margin="0,2"/>
                                                    <TextBlock Text="Circle" FontSize="9"/>
                                                </StackPanel>
                                            </Button>
                                        </StackPanel>
                                        <TextBlock Text="Yardımcı Çizim" Foreground="#888" FontSize="10" HorizontalAlignment="Center" Margin="0,4,0,0"/>
                                    </StackPanel>
                                </Border>
                            </WrapPanel>
                        </Border>
                    </TabItem>

                    <!-- ═══════ TAB 4: HESAPLAMA (Calculation) ═══════ -->
                    <TabItem Header="4. Hesap" x:Name="TabCalculation" IsEnabled="False">
                        <Border Background="#333337" Padding="6,4">
                            <WrapPanel>
                                <Border BorderBrush="#FF4444" BorderThickness="0,0,0,2" Margin="0,0,8,0" Padding="4,2">
                                    <StackPanel>
                                        <StackPanel Orientation="Horizontal">
                                            <Button Content="🛡️ Check System" Click="OnAuditSystem" Style="{StaticResource RbnWarn}" ToolTip="Açık uçları ve ters debileri bulur"/>
                                        </StackPanel>
                                        <TextBlock Text="Validasyon" Foreground="#666" FontSize="9" HorizontalAlignment="Center" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                                <Border BorderBrush="#0088FF" BorderThickness="0,0,0,2" Margin="0,0,8,0" Padding="4,2">
                                    <StackPanel>
                                        <StackPanel Orientation="Horizontal">
                                            <Button Content="📊 Sistem Analizi" Click="OnAutoPipeSizing" Style="{StaticResource RbnBtn}"/>
                                            <Button Content="⚡ HESAPLA" Click="OnRecalculateSystem" Style="{StaticResource RbnOk}" ToolTip="Tüm sistemi hesapla"/>
                                            <Button Content="🔄 Tesisat (Sadece)" Click="OnRecalculatePlumbing" Style="{StaticResource RbnBtn}"/>
                                        </StackPanel>
                                        <TextBlock Text="Sizing Motoru" Foreground="#666" FontSize="9" HorizontalAlignment="Center" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                                <Border BorderBrush="#FFFF00" BorderThickness="0,0,0,2" Margin="0,0,8,0" Padding="4,2">
                                    <StackPanel>
                                        <StackPanel Orientation="Horizontal">
                                            <Button Content="📉 Basınç Kaybı" Click="OnPressureDropCalc" Style="{StaticResource RbnBtn}"/>
                                            <Button Content="💧 Pompa" Click="OnPumpSelection" Style="{StaticResource RbnBtn}"/>
                                        </StackPanel>
                                        <TextBlock Text="Kritik Hat &amp; Cihaz" Foreground="#666" FontSize="9" HorizontalAlignment="Center" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                            </WrapPanel>
                        </Border>
                    </TabItem>

                    <!-- ═══════ TAB 5: RAPORLAR (Outputs) ═══════ -->
                    <TabItem Header="5. Rapor &amp; Çıktı" x:Name="TabOutputs" IsEnabled="False">
                        <Border Background="#333337" Padding="6,4">
                            <WrapPanel>
                                <Border BorderBrush="#44DD88" BorderThickness="0,0,0,2" Margin="0,0,8,0" Padding="4,2">
                                    <StackPanel>
                                        <StackPanel Orientation="Horizontal">
                                            <Button Content="✏️ Çapları Yazdır" Click="OnAutoAnnotate" Style="{StaticResource RbnOk}"/>
                                            <Button Content="🗑️ Temizle" Click="OnClearAnnotations" Style="{StaticResource RbnBtn}"/>
                                        </StackPanel>
                                        <TextBlock Text="UI Anotasyon" Foreground="#666" FontSize="9" HorizontalAlignment="Center" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                                <Border BorderBrush="#FFCC00" BorderThickness="0,0,0,2" Margin="0,0,8,0" Padding="4,2">
                                    <StackPanel>
                                        <StackPanel Orientation="Horizontal">
                                            <Button Content="📊 Hesaplama Tab." Click="OnCalculationTable" Style="{StaticResource RbnAccent}"/>
                                            <Button Content="📈 Hidrolik Rapor" Click="OnGenerateHydraulicReport" Style="{StaticResource RbnBtn}"/>
                                            <Button Content="📄 Sistem Raporu" Click="OnReportExport" Style="{StaticResource RbnBtn}" ToolTip="HTML/CSV/RTF"/>
                                        </StackPanel>
                                        <TextBlock Text="Föyler" Foreground="#666" FontSize="9" HorizontalAlignment="Center" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                                <Border BorderBrush="#AA66FF" BorderThickness="0,0,0,2" Margin="0,0,8,0" Padding="4,2">
                                    <StackPanel>
                                        <StackPanel Orientation="Horizontal">
                                            <Button Content="📐 Dikey (Kolon) Şema" Click="OnRiserDiagramExport" Style="{StaticResource RbnBtn}"/>
                                            <Button Content="📊 İzo-Şema" Click="OnShowIsometricScheme" Style="{StaticResource RbnBtn}"/>
                                        </StackPanel>
                                        <TextBlock Text="Şemalar" Foreground="#666" FontSize="9" HorizontalAlignment="Center" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                                <Border BorderBrush="#007ACC" BorderThickness="0,0,0,2" Margin="0,0,8,0" Padding="4,2">
                                    <StackPanel>
                                        <StackPanel Orientation="Horizontal">
                                            <Button Content="📜 Lejant" Click="OnGenerateLegend" Style="{StaticResource RbnBtn}"/>
                                            <Button Content="📝 Metraj (BOQ)" Click="OnGenerateBOM" Style="{StaticResource RbnOk}"/>
                                            <Button Content="📋 Şartname" Click="OnSpecificationExport" Style="{StaticResource RbnBtn}"/>
                                        </StackPanel>
                                        <TextBlock Text="Listeler" Foreground="#666" FontSize="9" HorizontalAlignment="Center" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                            </WrapPanel>
                        </Border>
                    </TabItem>

                    <!-- ═══════ TAB 6: GÖRÜNÜM ═══════ -->
                    <TabItem Header="👁️ Görünüm" x:Name="TabView">
                        <Border Background="#333337" Padding="6,4">
                            <WrapPanel>
                                <Border BorderBrush="#007ACC" BorderThickness="0,0,0,2" Margin="0,0,8,0" Padding="4,2">
                                    <StackPanel>
                                        <Button Content="🔎 Zoom Extents" Click="OnZoomExtents" Style="{StaticResource RbnBtn}"/>
                                        <TextBlock Text="Navigasyon" Foreground="#666" FontSize="9" HorizontalAlignment="Center" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                                <Border BorderBrush="#00DDFF" BorderThickness="0,0,0,2" Margin="0,0,8,0" Padding="4,2">
                                    <StackPanel>
                                        <StackPanel Orientation="Horizontal">
                                            <Button x:Name="View2DBtn" Content="2D" Click="OnToggle2DView" Style="{StaticResource RbnAccent}" Width="36"/>
                                            <Button x:Name="View3DBtn" Content="3D" Click="OnToggle3DView" Style="{StaticResource RbnBtn}" Width="36"/>
                                            <Button Content="🧊 3D Boru" Click="OnPipe3DView" Style="{StaticResource RbnBtn}"/>
                                        </StackPanel>
                                        <TextBlock Text="Mod" Foreground="#666" FontSize="9" HorizontalAlignment="Center" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                                <Border BorderBrush="#555" BorderThickness="0,0,0,2" Margin="0,0,8,0" Padding="4,2">
                                    <StackPanel>
                                        <StackPanel Orientation="Horizontal">
                                            <Button Content="📁 Navigator" Click="OnToggleProjectNavigator" Style="{StaticResource RbnBtn}"/>
                                            <Button Content="🧠 Intelligence" Click="OnToggleIntelligencePanel" Style="{StaticResource RbnBtn}"/>
                                        </StackPanel>
                                        <TextBlock Text="Paneller" Foreground="#666" FontSize="9" HorizontalAlignment="Center" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                            </WrapPanel>
                        </Border>
                    </TabItem>
"""

new_text = text[:start_idx] + new_tabs + "\n                " + text[end_idx:]

with open("src/Afney.Cad.Presentation/MainWindow.xaml", "w", encoding="utf-8") as f:
    f.write(new_text)

print("Rewrote MainWindow.xaml successfully.")

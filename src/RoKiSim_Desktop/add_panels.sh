#!/bin/bash

XAML_FILE="/home/main/roki_ws/src/RoKiSim_Desktop/MainWindow.axaml"

# The panels to inject
PANELS_XAML='
    <!-- FLOATING DATA PANELS -->
    <Border x:Name="pnlTorque" IsVisible="False" Background="#050505" BorderBrush="#e67e22" BorderThickness="2" CornerRadius="4" Width="320" Height="380" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="5,5,0,0" ZIndex="100">
        <Grid>
            <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
            <Grid Background="#221100" Padding="5">
                <TextBlock Text="[DATA] MOTOR LOAD &amp; TORQUE (%)" Foreground="#e67e22" FontFamily="Consolas" FontWeight="Bold"/>
                <Button x:Name="btnCloseTorque" Content="X" Background="#c0392b" Foreground="White" Width="20" Height="20" Padding="0" HorizontalAlignment="Right" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" FontSize="10" FontWeight="Bold"/>
            </Grid>
            <StackPanel Grid.Row="1" Margin="10" Spacing="15">
                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="70"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><TextBlock Text="J1 BASE" Foreground="White" FontFamily="Consolas" FontWeight="Bold" VerticalAlignment="Center"/><Grid Grid.Column="1" Height="20" Background="#111"><Border x:Name="barJ1" Background="#2ecc71" Width="30" HorizontalAlignment="Left"/></Grid></Grid>
                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="70"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><TextBlock Text="J2 SHLDR" Foreground="White" FontFamily="Consolas" FontWeight="Bold" VerticalAlignment="Center"/><Grid Grid.Column="1" Height="20" Background="#111"><Border x:Name="barJ2" Background="#f39c12" Width="180" HorizontalAlignment="Left"/></Grid></Grid>
                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="70"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><TextBlock Text="J3 ELBOW" Foreground="White" FontFamily="Consolas" FontWeight="Bold" VerticalAlignment="Center"/><Grid Grid.Column="1" Height="20" Background="#111"><Border x:Name="barJ3" Background="#2ecc71" Width="100" HorizontalAlignment="Left"/></Grid></Grid>
                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="70"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><TextBlock Text="J4 WRST1" Foreground="White" FontFamily="Consolas" FontWeight="Bold" VerticalAlignment="Center"/><Grid Grid.Column="1" Height="20" Background="#111"><Border x:Name="barJ4" Background="#2ecc71" Width="15" HorizontalAlignment="Left"/></Grid></Grid>
                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="70"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><TextBlock Text="J5 WRST2" Foreground="White" FontFamily="Consolas" FontWeight="Bold" VerticalAlignment="Center"/><Grid Grid.Column="1" Height="20" Background="#111"><Border x:Name="barJ5" Background="#2ecc71" Width="20" HorizontalAlignment="Left"/></Grid></Grid>
                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="70"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><TextBlock Text="J6 GRIP" Foreground="White" FontFamily="Consolas" FontWeight="Bold" VerticalAlignment="Center"/><Grid Grid.Column="1" Height="20" Background="#111"><Border x:Name="barJ6" Background="#2ecc71" Width="10" HorizontalAlignment="Left"/></Grid></Grid>
                <Grid Margin="70,5,0,0"><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="*"/><ColumnDefinition Width="*"/><ColumnDefinition Width="*"/><ColumnDefinition Width="*"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><TextBlock Grid.Column="0" Text="0" Foreground="#888" FontSize="9"/><TextBlock Grid.Column="1" Text="20" Foreground="#888" FontSize="9"/><TextBlock Grid.Column="2" Text="40" Foreground="#888" FontSize="9"/><TextBlock Grid.Column="3" Text="60" Foreground="#888" FontSize="9"/><TextBlock Grid.Column="4" Text="80" Foreground="#888" FontSize="9"/><TextBlock Grid.Column="5" Text="100" Foreground="#888" FontSize="9"/></Grid>
            </StackPanel>
        </Grid>
    </Border>

    <Border x:Name="pnlVelocity" IsVisible="False" Background="#050505" BorderBrush="#9b59b6" BorderThickness="2" CornerRadius="4" Width="320" Height="280" HorizontalAlignment="Left" VerticalAlignment="Bottom" Margin="5,0,0,5" ZIndex="100">
        <Grid>
            <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
            <Grid Background="#1a001a" Padding="5">
                <TextBlock Text="[DATA] TCP KINEMATIC VELOCITY (m/s)" Foreground="#9b59b6" FontFamily="Consolas" FontWeight="Bold"/>
                <Button x:Name="btnCloseVelocity" Content="X" Background="#c0392b" Foreground="White" Width="20" Height="20" Padding="0" HorizontalAlignment="Right" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" FontSize="10" FontWeight="Bold"/>
            </Grid>
            <Grid Grid.Row="1" Margin="10,10,10,20">
                <Grid.ColumnDefinitions><ColumnDefinition Width="30"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                <StackPanel Grid.Column="0" VerticalAlignment="Stretch" Spacing="3">
                    <TextBlock Text="1.0" Foreground="#888" FontSize="10" Height="15"/><TextBlock Text="0.9" Foreground="#888" FontSize="10" Height="15"/><TextBlock Text="0.8" Foreground="#888" FontSize="10" Height="15"/><TextBlock Text="0.7" Foreground="#888" FontSize="10" Height="15"/><TextBlock Text="0.6" Foreground="#888" FontSize="10" Height="15"/><TextBlock Text="0.5" Foreground="#888" FontSize="10" Height="15"/><TextBlock Text="0.4" Foreground="#888" FontSize="10" Height="15"/><TextBlock Text="0.3" Foreground="#888" FontSize="10" Height="15"/><TextBlock Text="0.2" Foreground="#888" FontSize="10" Height="15"/><TextBlock Text="0.1" Foreground="#888" FontSize="10" Height="15"/><TextBlock Text="0" Foreground="#888" FontSize="10" Height="15"/>
                </StackPanel>
                <Canvas Grid.Column="1" Background="#111" ClipToBounds="True">
                    <Polyline x:Name="polyVelocity" Stroke="#9b59b6" StrokeThickness="2" />
                </Canvas>
            </Grid>
        </Grid>
    </Border>

    <Border x:Name="pnlPosition" IsVisible="False" Background="#050505" BorderBrush="#3498db" BorderThickness="2" CornerRadius="4" Width="450" Height="700" HorizontalAlignment="Center" VerticalAlignment="Center" ZIndex="100">
        <Grid>
            <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
            <Grid Background="#001122" Padding="5">
                <TextBlock Text="[DATA] JOINT POSITIONS (DEG)" Foreground="#3498db" FontFamily="Consolas" FontWeight="Bold"/>
                <Button x:Name="btnClosePosition" Content="X" Background="#c0392b" Foreground="White" Width="20" Height="20" Padding="0" HorizontalAlignment="Right" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" FontSize="10" FontWeight="Bold"/>
            </Grid>
            <StackPanel Grid.Row="1" Margin="10" Spacing="8">
                <TextBlock Text="> MOTOR J1 - BASE POSITION" Foreground="#3498db" FontFamily="Consolas" FontSize="11" FontWeight="Bold"/>
                <Border Height="60" Background="#111" BorderBrush="#222" BorderThickness="1"><Canvas ClipToBounds="True"><Polyline x:Name="polyJ1" Stroke="#3498db" StrokeThickness="2"/></Canvas></Border>
                <TextBlock Text="> MOTOR J2 - SHOULDER POSITION" Foreground="#3498db" FontFamily="Consolas" FontSize="11" FontWeight="Bold"/>
                <Border Height="60" Background="#111" BorderBrush="#222" BorderThickness="1"><Canvas ClipToBounds="True"><Polyline x:Name="polyJ2" Stroke="#3498db" StrokeThickness="2"/></Canvas></Border>
                <TextBlock Text="> MOTOR J3 - ELBOW POSITION" Foreground="#3498db" FontFamily="Consolas" FontSize="11" FontWeight="Bold"/>
                <Border Height="60" Background="#111" BorderBrush="#222" BorderThickness="1"><Canvas ClipToBounds="True"><Polyline x:Name="polyJ3" Stroke="#3498db" StrokeThickness="2"/></Canvas></Border>
                <TextBlock Text="> MOTOR J4 - WRIST ROLL" Foreground="#3498db" FontFamily="Consolas" FontSize="11" FontWeight="Bold"/>
                <Border Height="60" Background="#111" BorderBrush="#222" BorderThickness="1"><Canvas ClipToBounds="True"><Polyline x:Name="polyJ4" Stroke="#3498db" StrokeThickness="2"/></Canvas></Border>
                <TextBlock Text="> MOTOR J5 - WRIST PITCH" Foreground="#3498db" FontFamily="Consolas" FontSize="11" FontWeight="Bold"/>
                <Border Height="60" Background="#111" BorderBrush="#222" BorderThickness="1"><Canvas ClipToBounds="True"><Polyline x:Name="polyJ5" Stroke="#3498db" StrokeThickness="2"/></Canvas></Border>
                <TextBlock Text="> MOTOR J6 - FLANGE" Foreground="#3498db" FontFamily="Consolas" FontSize="11" FontWeight="Bold"/>
                <Border Height="60" Background="#111" BorderBrush="#222" BorderThickness="1"><Canvas ClipToBounds="True"><Polyline x:Name="polyJ6" Stroke="#3498db" StrokeThickness="2"/></Canvas></Border>
            </StackPanel>
        </Grid>
    </Border>
'

sed -i '/<\/Grid>/,$!b;//{x;//p;g;s/.*/'"${PANELS_XAML//$'\n'/\\n}"'/;p;x;d;}' "$XAML_FILE"

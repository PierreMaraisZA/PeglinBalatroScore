param([string]$OutPath)

# Draws the PeglinBalatroScore icon as 64x64 pixel art (two score boxes with fire columns, like the mod) and scales
# it up 4x with hard pixel edges to 256x256 for Thunderstore.

Add-Type -AssemblyName System.Drawing

$size = 64
$bmp = New-Object System.Drawing.Bitmap $size, $size
function C($r, $g, $b) { [System.Drawing.Color]::FromArgb(255, $r, $g, $b) }
function Px($x, $y, $c) { if ($x -ge 0 -and $x -lt $size -and $y -ge 0 -and $y -lt $size) { $bmp.SetPixel($x, $y, $c) } }
function Rect($x0, $y0, $x1, $y1, $c) { for ($y = $y0; $y -le $y1; $y++) { for ($x = $x0; $x -le $x1; $x++) { Px $x $y $c } } }

# The game's dark navy behind the score, with a few faint pegs.
Rect 0 0 63 63 (C 16 16 31)
$pegs = @(@(8, 8), @(20, 4), @(44, 6), @(56, 12), @(32, 2), @(12, 20), @(52, 23))
foreach ($p in $pegs) { Rect $p[0] $p[1] ($p[0] + 1) ($p[1] + 1) (C 34 34 58) }

# Same fire as the mod: heat rises from the bottom row, cooling and drifting sideways at random.
function Fire($width, $height, $reach, $seed) {
    $rand = New-Object System.Random $seed
    $heat = New-Object 'double[,]' $width, $height
    for ($step = 0; $step -lt 90; $step++) {
        for ($x = 0; $x -lt $width; $x++) {
            # Hotter in the middle so the column tapers to a point instead of rising as a block.
            $edge = [Math]::Abs(($x + 0.5) / $width * 2 - 1)
            $heat[$x, 0] = (0.85 + $rand.NextDouble() * 0.15) * (1 - 0.55 * $edge * $edge)
        }
        $cool = 1.0 / $reach
        for ($y = $height - 1; $y -ge 1; $y--) {
            for ($x = 0; $x -lt $width; $x++) {
                $sx = $x + $rand.Next(-1, 2)
                if ($sx -lt 0 -or $sx -ge $width) { $heat[$x, $y] = 0; continue }
                $v = $heat[$sx, ($y - 1)] - $rand.NextDouble() * 2 * $cool
                # 0.0, not 0: PowerShell would otherwise pick Max(int, int) and round the heat to whole numbers.
                $heat[$x, $y] = [Math]::Max(0.0, $v)
            }
        }
    }
    return ,$heat
}

function DrawFire($left, $baseY, $width, $height, $reach, $seed, $palette) {
    $heat = Fire $width $height $reach $seed
    for ($y = 0; $y -lt $height; $y++) {
        for ($x = 0; $x -lt $width; $x++) {
            $h = $heat[$x, $y]
            if ($h -lt 0.1) { continue }
            $shade = [Math]::Min($palette.Count - 1, [int](($h - 0.1) / 0.9 * $palette.Count))
            Px ($left + $x) ($baseY - $y) $palette[$shade]
        }
    }
}

# The mod's own flame palettes (ScoreColours.PegsFlame and DamageFlame), stepped into 7 flat shades like PixelFlame.
function Shades($stops) {
    $out = @()
    for ($i = 0; $i -lt 7; $i++) {
        $t = $i / 6 * ($stops.Count - 1)
        $s = [Math]::Min([int][Math]::Floor($t), $stops.Count - 2)
        $f = $t - $s
        $a = $stops[$s]; $b = $stops[$s + 1]
        $out += C ([int]($a[0] + ($b[0] - $a[0]) * $f)) ([int]($a[1] + ($b[1] - $a[1]) * $f)) ([int]($a[2] + ($b[2] - $a[2]) * $f))
    }
    return ,$out
}
$green = Shades @(@(66, 92, 24), @(124, 168, 44), @(190, 226, 84), @(246, 252, 190))
$gold = Shades @(@(150, 54, 16), @(226, 120, 24), @(250, 196, 50), @(255, 246, 180))

# Fire columns rooted just inside the top of each box.
DrawFire 4 41 25 40 30 11 $green
DrawFire 35 41 25 40 38 23 $gold

# A score box: dark outline, darker lip at the bottom, raised face on top.
function Box($x0, $y0, $x1, $y1, $face, $lip) {
    Rect ($x0 - 1) ($y0 - 1) ($x1 + 1) ($y1 + 1) (C 24 18 16)
    Rect $x0 $y0 $x1 $y1 $lip
    Rect $x0 $y0 $x1 ($y1 - 3) $face
}
Box 4 39 28 54 (C 112 142 46) (C 70 92 26)
Box 35 39 59 54 (C 222 170 30) (C 150 104 14)

# The "x" between the boxes, in crit yellow.
$x = C 222 170 30
foreach ($d in 0..3) { Px (30 + $d) (44 + $d) $x; Px (33 - $d) (44 + $d) $x }

# 3x5 pixel digits, drawn 2x with a dark drop shadow.
$font = @{
    '1' = @('010', '110', '010', '010', '111');
    '2' = @('111', '001', '111', '100', '111');
    '4' = @('101', '101', '111', '001', '001');
    '7' = @('111', '001', '010', '010', '010')
}
function Digit($ch, $left, $top) {
    $rows = $font[$ch]
    for ($r = 0; $r -lt 5; $r++) {
        for ($c = 0; $c -lt 3; $c++) {
            if ($rows[$r][$c] -eq '1') {
                Rect ($left + $c * 2 + 1) ($top + $r * 2 + 1) ($left + $c * 2 + 2) ($top + $r * 2 + 2) (C 28 22 20)
                Rect ($left + $c * 2) ($top + $r * 2) ($left + $c * 2 + 1) ($top + $r * 2 + 1) (C 255 255 255)
            }
        }
    }
}
# Left box right-aligned, right box left-aligned, like the mod.
Digit '1' 12 41
Digit '2' 20 41
Digit '7' 38 41

# Scale up 4x with hard pixel edges.
$out = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($out)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.DrawImage($bmp, 0, 0, 256, 256)
$g.Dispose()
$out.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$out.Dispose()
$bmp.Dispose()
"Saved $OutPath"

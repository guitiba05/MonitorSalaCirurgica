"""Gera os STLs da caixa do Monitor de Sala Cirúrgica (medidas em mm).
Gerador paramétrico da caixa: janela do DHT11, grade do buzzer e ventilação atrás.

    pip install manifold3d trimesh
    python gerar_stls.py

Todas as medidas ficam nas constantes abaixo: se o ESP32 ou a impressora de vocês
pedirem outra folga, mudem aqui e rodem de novo.
"""
import math
import os

import numpy as np
import trimesh
from manifold3d import CrossSection, JoinType, Manifold

SEG = 96  # segmentos dos círculos (suavidade das curvas)

# ------------------------------------------------------------------ medidas
W, D, H = 116.0, 68.0, 46.0      # base: largura (x), profundidade (y), altura (z)
R = 10.0                          # raio dos cantos externos
WALL, FLOOR = 2.0, 2.0            # parede e fundo
IW, ID = W - 2 * WALL, D - 2 * WALL
RI = R - WALL                     # raio dos cantos internos

LID_T = 4.0                       # espessura da tampa
GAP = 0.15                        # folga do encaixe de pressão (por lado)
LEDGE_W = 1.5                     # largura do rebaixo onde a tampa apoia
LEDGE_TOP = H - LID_T             # a tampa apoia em z = 42 e fica nivelada com a borda

USB_W, USB_H, USB_Z = 12.0, 8.0, 23.0           # saída do USB (lateral -x = direita de quem olha a frente)
LDR_D, LDR_X, LDR_Z = 5.2, 30.0, 26.0           # furo do LDR (frente, +y)
NOTCH_W, NOTCH_H = 14.0, 6.0                    # recorte para o dedo (frente, borda de cima)
VENT_W, VENT_H, VENT_Z, VENT_N, VENT_STEP = 2.4, 16.0, 27.0, 5, 6.0  # ventilação (parede de trás, -y)
DHT_WIN_W, DHT_WIN_H, DHT_Z = 16.5, 13.0, 24.0  # janela do DHT11 (lateral +x = esquerda de quem olha a frente): o sensor aparece por ela
DHT_PCB_W = 28.5                                # largura da placa do módulo (guias por dentro, dos dois lados)
BZ_X, BZ_Z, BZ_D = -30.0, 24.0, 12.4            # buzzer (frente, +y; à direita de quem olha a frente): anel que segura o buzzer de 12 mm
BZ_HOLE, BZ_RING_H = 1.8, 4.0                   # furos da grade do som e altura do anel

CRADLE_Z = 20.0                   # altura do apoio do ESP32 (a placa fica com espaço embaixo para os jumpers)
ESP_X0, ESP_X1 = -49.75, 1.75     # ponta do USB e ponta da antena da placa (51,5 mm)

RING_RO, RING_RI, RING_H = 28.0, 25.5, 6.0      # anel da tampa onde a cúpula trava
TAB_W, TAB_R, TAB_Z0, TAB_Z1 = 5.0, 29.8, 2.5, 4.5  # 3 dentes do anel (z medido a partir do topo da tampa)
BOSS_R, BOSS_H, LED_HOLE = 5.0, 5.0, 4.6        # suporte do LED (furo só para as pernas)

DOME_GAP = 0.3                    # folga entre a cúpula e o anel
SKIRT_RI = RING_RO + DOME_GAP     # 28,3
SKIRT_RO = 31.3
SKIRT_H = 8.0
SHELL = 1.2                       # parede da cúpula
TWIST_DEG = 45.0                  # giro para travar (⅛ de volta)

OUT = os.path.dirname(os.path.abspath(__file__))


# ------------------------------------------------------------------ helpers
def rrect(w, d, r):
    """Retângulo de cantos arredondados, centrado na origem."""
    return CrossSection.square([w - 2 * r, d - 2 * r], center=True).offset(r, JoinType.Round, circular_segments=SEG)


def prism(cs, z0, z1):
    return Manifold.extrude(cs, z1 - z0).translate([0, 0, z0])


def box(x0, x1, y0, y1, z0, z1):
    return Manifold.cube([x1 - x0, y1 - y0, z1 - z0]).translate([x0, y0, z0])


def cyl(r, z0, z1, x=0.0, y=0.0):
    return Manifold.cylinder(z1 - z0, r, r, SEG).translate([x, y, z0])


def slab(cs, z):
    """Fatia finíssima de um perfil, usada para fazer chanfros com hull."""
    return prism(cs, z, z + 0.01)


def export(m, name):
    m = m.simplify(0.001)   # remove triângulos degenerados (sem mudar a forma)
    mesh = m.to_mesh()
    tm = trimesh.Trimesh(vertices=np.asarray(mesh.vert_properties)[:, :3], faces=np.asarray(mesh.tri_verts),
                         process=False)
    path = os.path.join(OUT, name)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    tm.export(path)
    b = tm.bounds
    print(f"{name:42s} {b[1][0]-b[0][0]:6.1f} x {b[1][1]-b[0][1]:5.1f} x {b[1][2]-b[0][2]:5.1f} mm"
          f"  volume {tm.volume/1000:5.1f} cm³  estanque={tm.is_watertight}")
    return tm


# ================================================================== BASE
def make_base():
    body = prism(rrect(W, D, R), 0, H)
    cavity = prism(rrect(IW, ID, RI), FLOOR, H + 1)
    base = body - cavity

    # rebaixo: degrau interno de 1,5 mm (z 38 a 42) com chanfro de 45° embaixo (imprime sem suporte)
    inner_open = rrect(IW - 2 * LEDGE_W, ID - 2 * LEDGE_W, RI - LEDGE_W)
    ledge = prism(rrect(IW, ID, RI), LEDGE_TOP - 4, LEDGE_TOP) - prism(inner_open, LEDGE_TOP - 4.1, LEDGE_TOP + 0.1)
    chamfer_cut = Manifold.batch_hull([slab(rrect(IW, ID, RI), LEDGE_TOP - 4 - LEDGE_W - 0.01),
                                       slab(inner_open, LEDGE_TOP - 4)])
    chamfer = prism(rrect(IW, ID, RI), LEDGE_TOP - 4 - LEDGE_W, LEDGE_TOP - 4) - chamfer_cut
    base = base + ledge + chamfer

    # berço do ESP32: coluna inteira na ponta da antena (com batente) e duas colunas na ponta do USB
    base += box(ESP_X1 - 2, ESP_X1 + 2, -16, 16, FLOOR - 0.1, CRADLE_Z)
    base += box(ESP_X1 + 0.6, ESP_X1 + 2, -16, 16, CRADLE_Z - 0.1, CRADLE_Z + 3)
    for y in (-12.0, 12.0):
        base += box(ESP_X0 - 1, ESP_X0 + 3, y - 2.5, y + 2.5, FLOOR - 0.1, CRADLE_Z)

    # recortes
    base -= box(-W / 2 - 1, -W / 2 + WALL + 1, -USB_W / 2, USB_W / 2, USB_Z - USB_H / 2, USB_Z + USB_H / 2)
    base -= Manifold.cylinder(WALL + 2, LDR_D / 2, LDR_D / 2, SEG).rotate([-90, 0, 0]).translate([LDR_X, D / 2 - WALL - 1, LDR_Z])
    # "U" com fundo arredondado: 14 mm de largura, 6 mm para baixo a partir da borda
    notch_cs = (rrect(NOTCH_W, 16, 3).translate([0, 5])
                ^ CrossSection.square([NOTCH_W, NOTCH_H + 2], center=True).translate([0, 1]))
    notch_solid = Manifold.extrude(notch_cs, WALL + 0.4).rotate([90, 0, 0]).translate([0, D / 2 + 0.2, H - NOTCH_H / 2])
    base -= notch_solid
    # ventilação na parede de trás (-y)
    for i in range(VENT_N):
        x = (i - (VENT_N - 1) / 2) * VENT_STEP
        base -= box(x - VENT_W / 2, x + VENT_W / 2, -D / 2 - 1, -D / 2 + WALL + 1, VENT_Z - VENT_H / 2, VENT_Z + VENT_H / 2)

    # DHT11: janela na lateral direita (+x) e duas guias por dentro para a placa do módulo deslizar de cima
    base -= box(W / 2 - WALL - 1, W / 2 + 1, -DHT_WIN_W / 2, DHT_WIN_W / 2, DHT_Z - DHT_WIN_H / 2, DHT_Z + DHT_WIN_H / 2)
    for sy in (-1, 1):
        y_in = sy * (DHT_PCB_W / 2 + 0.3)
        y_out = sy * (DHT_PCB_W / 2 + 1.8)
        rib = box(W / 2 - WALL - 4.0, W / 2 - WALL + 0.1, min(y_in, y_out), max(y_in, y_out), DHT_Z - 11, DHT_Z + 12)
        lip = box(W / 2 - WALL - 4.0, W / 2 - WALL - 2.4, min(y_in, y_out) - (1.2 if sy > 0 else 0),
                  max(y_in, y_out) + (0 if sy > 0 else 1.2), DHT_Z - 11, DHT_Z + 12)
        base += rib + lip
    # abre o degrau da tampa acima das guias, para o módulo descer pelo canal
    base -= box(W / 2 - WALL - 4.2, W / 2 - WALL + 0.05, -(DHT_PCB_W / 2 + 0.3), DHT_PCB_W / 2 + 0.3, DHT_Z + 11.9, H + 1)

    # buzzer: grade de 7 furos na frente (+y) e anel por dentro que segura o buzzer de 12 mm
    furos = [(0.0, 0.0)] + [(3.4 * math.cos(math.radians(a)), 3.4 * math.sin(math.radians(a))) for a in range(0, 360, 60)]
    for fx, fz in furos:
        base -= Manifold.cylinder(WALL + 2, BZ_HOLE / 2, BZ_HOLE / 2, 24).rotate([-90, 0, 0]).translate(
            [BZ_X + fx, D / 2 - WALL - 1, BZ_Z + fz])
    anel = (Manifold.cylinder(BZ_RING_H, BZ_D / 2 + 1.2, BZ_D / 2 + 1.2, SEG)
            - Manifold.cylinder(BZ_RING_H + 1, BZ_D / 2, BZ_D / 2, SEG).translate([0, 0, -0.5]))
    base += anel.rotate([90, 0, 0]).translate([BZ_X, D / 2 - WALL + 0.1, BZ_Z])
    return base


# ================================================================== TAMPA
def tab(angle_deg):
    """Dente do anel com a parte de baixo chanfrada (imprime sem suporte)."""
    t = TAB_W / 2
    a = box(RING_RO - 0.6, RING_RO + 0.2, -t, t, LID_T + TAB_Z0, LID_T + TAB_Z1)
    b = box(RING_RO - 0.6, TAB_R, -t, t, LID_T + TAB_Z1 - 0.2, LID_T + TAB_Z1)
    return Manifold.batch_hull([a, b]).rotate([0, 0, angle_deg])


def make_lid():
    plate = prism(rrect(IW - 2 * GAP, ID - 2 * GAP, RI - GAP), 0, LID_T)
    ring = cyl(RING_RO, LID_T - 0.1, LID_T + RING_H) - cyl(RING_RI, LID_T - 0.2, LID_T + RING_H + 1)
    boss = cyl(BOSS_R, LID_T - 0.1, LID_T + BOSS_H)
    lid = plate + ring + boss
    for k in range(3):
        lid += tab(k * 120)
    lid -= cyl(LED_HOLE / 2, -1, LID_T + BOSS_H + 1)
    return lid


# ================================================================== CÚPULA
def make_dome():
    # perfil externo (raio, altura) a partir do topo da saia; inclinação máxima de ~50° e topo plano
    outer = [(SKIRT_RO, SKIRT_H), (31.6, 16), (31.8, 24), (31.0, 31), (28.8, 37.5), (25.4, 43), (21.0, 48),
             (15.8, 52.2), (10.0, 55.4), (6.4, 56.6), (0.0, 56.8)]
    # perfil espelhado dos dois lados do eixo: assim a parede não "engrossa" no centro
    right = [(SKIRT_RO, -5.0)] + outer[:-1]
    left = [(-r, z) for r, z in reversed(right)]
    region = CrossSection([right + [outer[-1]] + left])
    inner = region.offset(-SHELL, JoinType.Round, circular_segments=SEG)
    shell_cs = (region - inner) ^ CrossSection.square([SKIRT_RO + 5, 80]).translate([0, SKIRT_H])
    skirt_cs = CrossSection.square([SKIRT_RO - SKIRT_RI, SKIRT_H + 0.5]).translate([SKIRT_RI, 0])
    # saia e corpo num perfil só, girado uma vez: sem emenda entre as duas partes
    dome = Manifold.revolve(shell_cs + skirt_cs, SEG * 2)

    # baioneta: 3 canais em L por dentro da saia (entrada vertical + giro de 45°)
    groove_r = TAB_R + 0.3
    half_w = math.degrees((TAB_W / 2 + 0.3) / SKIRT_RI)
    for k in range(3):
        a0 = k * 120.0
        ring_cut = cyl(groove_r, -1, TAB_Z1 + 0.3) - cyl(SKIRT_RI - 1, -2, SKIRT_H)
        # entrada vertical
        wedge = sector(a0 - half_w, a0 + half_w, -1, TAB_Z1 + 0.3)
        dome -= ring_cut ^ wedge
        # canal horizontal (gira no sentido horário visto de cima)
        wedge2 = sector(a0 - half_w - TWIST_DEG, a0 + half_w, TAB_Z0 - 0.3, TAB_Z1 + 0.3)
        dome -= (cyl(groove_r, TAB_Z0 - 0.3, TAB_Z1 + 0.3) - cyl(SKIRT_RI - 1, -2, SKIRT_H)) ^ wedge2
    return dome


def sector(a0, a1, z0, z1, r=60.0):
    """Fatia de pizza (em graus) entre z0 e z1."""
    pts = [(0.0, 0.0)]
    n = max(4, int(abs(a1 - a0) / 3))
    for i in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * i / n)
        pts.append((r * math.cos(a), r * math.sin(a)))
    return prism(CrossSection([pts]), z0, z1)


# ================================================================== TESTES
def make_tests(base, lid):
    corner_base = base ^ box(24, W / 2 + 1, 2, D / 2 + 1, 28, H + 1)
    corner_base = corner_base.translate([0, 0, -28])
    corner_lid = lid ^ box(24, W / 2, 2, D / 2, -1, LID_T)
    disc = cyl(15, 0, SHELL)
    return corner_base, corner_lid, disc


if __name__ == "__main__":
    base, lid, dome = make_base(), make_lid(), make_dome()
    export(base, "1_base_PRETO.stl")  # janela do DHT11, grade do buzzer, LDR, USB e ventilação
    export(lid, "2_tampa_PRETO.stl")
    export(dome, "3_cupula_BRANCO.stl")
    cb, cl, disc = make_tests(base, lid)
    export(cb, "testes/teste_encaixe_canto_da_base_PRETO.stl")
    export(cl, "testes/teste_encaixe_canto_da_tampa_PRETO.stl")
    export(disc, "testes/teste_luz_disco_BRANCO.stl")

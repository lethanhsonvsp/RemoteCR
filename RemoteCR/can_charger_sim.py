import can
import time
import threading

# ============================================================
# CONFIG
# ============================================================
CAN_IFACE = "can0"
TX_PERIOD = 0.1
WATCHDOG_TIMEOUT = 0.3

# ============================================================
# STATE – CHARGER MODEL (GIỮ LOGIC CŨ)
# ============================================================
state = {
    # battery / output
    "battery_voltage": 48.0,
    "output_voltage": 48.0,
    "output_current": 0.0,

    # command từ 0x191
    "demand_v": 0.0,
    "demand_i": 0.0,

    # ⚠️ LOGIC FLAG (KHÔNG PHẢI BIT CAN)
    "power_enable": False,     # alias logic
    "power_stage1": False,     # bit CAN thật

    # wireless
    "gap_mm": 8,
    "wireless_ok": True,

    # watchdog
    "has_seen_191": False,
    "last_191_time": 0.0,
    "fault_watchdog": False,

    # runtime
    "charging": False,
}

# ============================================================
# CAN BUS
# ============================================================
bus = can.interface.Bus(channel=CAN_IFACE, interface="socketcan")

# ============================================================
# BIT HELPERS (LSB-first)
# ============================================================
def get_bits(buf, start, length):
    v = 0
    for i in range(length):
        bit = start + i
        if buf[bit // 8] & (1 << (bit % 8)):
            v |= 1 << i
    return v

def set_bits(buf, start, length, value):
    for i in range(length):
        bit = start + i
        if (value >> i) & 1:
            buf[bit // 8] |= (1 << (bit % 8))
        else:
            buf[bit // 8] &= ~(1 << (bit % 8))

# ============================================================
# RX LOOP – CONTROL MODULE 0x191 (FIX BIT)
# ============================================================
def rx_loop():
    while True:
        msg = bus.recv()
        if msg.arbitration_id != 0x191:
            continue

        d = msg.data

        state["has_seen_191"] = True
        state["last_191_time"] = time.time()

        # ---- decode ĐÚNG TÀI LIỆU ----
        state["demand_v"] = get_bits(d, 0, 20) * 0.001
        state["power_stage1"] = get_bits(d, 20, 1) == 1     # ✅ BIT ĐÚNG
        state["demand_i"] = get_bits(d, 32, 18) * 0.001

        # ---- GIỮ LOGIC CŨ ----
        # power_enable không tồn tại trong CAN
        # → ánh xạ logic
        state["power_enable"] = state["power_stage1"]

        print(
            f"[RX 0x191] "
            f"V={state['demand_v']:.1f}V "
            f"I={state['demand_i']:.1f}A "
            f"EN={state['power_enable']} "
            f"ST1={state['power_stage1']}"
        )

# ============================================================
# SAFE SEND
# ============================================================
def send(can_id, data):
    try:
        bus.send(can.Message(
            arbitration_id=can_id,
            data=data,
            is_extended_id=False
        ))
    except can.CanOperationError:
        pass

# ============================================================
# TX FRAMES
# ============================================================
def tx_311():
    d = bytearray(8)
    set_bits(d, 0, 20, int(state["output_voltage"] / 0.001))
    set_bits(d, 20, 18, int(state["output_current"] / 0.001))
    set_bits(d, 39, 1, int(state["charging"]))
    send(0x311, d)

def tx_321():
    d = bytearray(8)

    if state["fault_watchdog"]:
        charger_state = 0x10
    elif state["charging"]:
        charger_state = 0x04
    else:
        charger_state = 0x01

    set_bits(d, 0, 6, charger_state)
    set_bits(d, 12, 1, int(state["fault_watchdog"]))
    set_bits(d, 24, 1, int(state["fault_watchdog"]))
    send(0x321, d)

def tx_3c1():
    d = bytearray(8)
    set_bits(d, 0, 20, int(230 / 0.001))
    set_bits(d, 20, 18, int(4 / 0.001))
    set_bits(d, 38, 10, int(50 / 0.1))
    send(0x3C1, d)

def tx_3e1():
    d = bytearray(8)
    set_bits(d, 16, 10, int(92 / 0.1))
    set_bits(d, 32, 8, state["gap_mm"])
    send(0x3E1, d)

def tx_3f1():
    d = bytearray(8)
    set_bits(d, 0, 16, int(35 / 0.005))
    set_bits(d, 16, 16, int(42 / 0.005))
    send(0x3F1, d)

def tx_5f1():
    d = bytearray(8)
    set_bits(d, 7, 1, int(state["wireless_ok"]))
    set_bits(d, 6, 1, int(not state["charging"]))
    send(0x5F1, d)

# ============================================================
# MAIN LOOP – CHARGER LOGIC (GIỮ NGUYÊN)
# ============================================================
def tx_loop():
    while True:
        now = time.time()

        # ---------- ACTIVE REQUEST (LOGIC CŨ) ----------
        active_request = (
            state["power_enable"]
            and state["power_stage1"]
            and state["demand_i"] > 0
        )

        # ---------- WATCHDOG ----------
        if state["has_seen_191"] and active_request:
            state["fault_watchdog"] = (
                now - state["last_191_time"] > WATCHDOG_TIMEOUT
            )
        else:
            state["fault_watchdog"] = False

        # ---------- WIRELESS ----------
        state["wireless_ok"] = state["gap_mm"] <= 20

        # ---------- CHARGING CONDITION ----------
        allow_charge = (
            not state["fault_watchdog"]
            and active_request
            and state["wireless_ok"]
            and state["battery_voltage"] > 10
        )

        if allow_charge:
            state["charging"] = True

            if state["output_voltage"] < state["demand_v"]:
                state["output_current"] = min(state["demand_i"], 10.0)
                state["output_voltage"] += 0.02
            else:
                state["output_current"] = max(
                    0.0, state["output_current"] - 0.1
                )
        else:
            state["charging"] = False
            state["output_current"] = 0.0

        # ---------- TX ----------
        tx_311()
        tx_321()
        tx_3c1()
        tx_3e1()
        tx_3f1()
        tx_5f1()

        time.sleep(TX_PERIOD)

# ============================================================
# START
# ============================================================
print("🚀 Wireless Charger CAN Simulator (FIXED BIT, OLD LOGIC KEPT)")

threading.Thread(target=rx_loop, daemon=True).start()
tx_loop()

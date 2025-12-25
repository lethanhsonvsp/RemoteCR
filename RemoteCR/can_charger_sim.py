import can
import time
import threading
import struct

# ================= CONFIG =================
CAN_IFACE = "can0"
TX_PERIOD = 0.1   # 100 ms
UNIT = 1          # x = 1 → 0x3x1, 0x31x...

# ================= STATE =================
state = {
    "voltage": 48.5,
    "current": 0.0,
    "demand_v": 0.0,
    "demand_i": 0.0,
    "power_enable": False,
    "charging": False,
    "gap": 8,
}

# ================= CAN BUS =================
bus = can.interface.Bus(channel=CAN_IFACE, interface="socketcan")

# ================= BIT HELPERS =================
def set_bits(buf, start, length, value):
    for i in range(length):
        bit = start + i
        if (value >> i) & 1:
            buf[bit // 8] |= (1 << (bit % 8))
        else:
            buf[bit // 8] &= ~(1 << (bit % 8))

# ================= RX HANDLER =================
def rx_loop():
    while True:
        msg = bus.recv()
        if msg.arbitration_id == 0x191:
            d = msg.data
            state["demand_v"] = int.from_bytes(d[0:3], "little") * 0.001
            state["power_enable"] = (d[2] >> 4) & 1
            state["demand_i"] = ((d[4] | (d[5] << 8)) & 0x3FFFF) * 0.001

            state["charging"] = (
                state["power_enable"] and state["demand_i"] > 0
            )

# ================= TX HELPERS =================
def send(id, data):
    bus.send(can.Message(arbitration_id=id, data=data, is_extended_id=False))

def tx_311():
    d = bytearray(8)
    set_bits(d, 0, 20, int(state["voltage"] / 0.001))
    set_bits(d, 20, 18, int(state["current"] / 0.001))
    set_bits(d, 38, 1, 0)
    set_bits(d, 39, 1, int(state["charging"]))
    send(0x311, d)

def tx_321():
    d = bytearray(8)
    st = 0x04 if state["charging"] else 0x01
    set_bits(d, 0, 6, st)
    send(0x321, d)

def tx_3c1():
    d = bytearray(8)
    set_bits(d, 0, 20, int(230 / 0.001))
    set_bits(d, 20, 18, int(4 / 0.001))
    set_bits(d, 38, 10, int(50 / 0.1))
    send(0x3C1, d)

def tx_3e1():
    d = bytearray(8)
    set_bits(d, 16, 10, int(92 / 0.1))     # efficiency
    set_bits(d, 32, 8, state["gap"])
    send(0x3E1, d)

def tx_3f1():
    d = bytearray(8)
    set_bits(d, 0, 16, int(35 / 0.005))
    set_bits(d, 16, 16, int(42 / 0.005))
    send(0x3F1, d)

def tx_5f1():
    d = bytearray(8)
    wireless_ok = state["gap"] <= 20
    set_bits(d, 7, 1, int(wireless_ok))
    set_bits(d, 6, 1, 0)
    send(0x5F1, d)

# ================= MAIN LOOP =================
def tx_loop():
    while True:
        if state["charging"]:
            state["current"] = min(state["demand_i"], 10.0)
        else:
            state["current"] = 0.0

        tx_311()
        tx_321()
        tx_3c1()
        tx_3e1()
        tx_3f1()
        tx_5f1()

        time.sleep(TX_PERIOD)

# ================= START =================
threading.Thread(target=rx_loop, daemon=True).start()
tx_loop()

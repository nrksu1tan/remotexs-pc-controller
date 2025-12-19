using System;
using System.Runtime.InteropServices;

namespace XrdRemote
{
    public static class KeyboardModule
    {
        // --- WINAPI STRUCTS ---
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public InputUnion U;
            public static int Size => Marshal.SizeOf(typeof(INPUT));
        }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT { public uint uMsg; public ushort wParamL; public ushort wParamH; }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);

        const int INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint KEYEVENTF_SCANCODE = 0x0008;

        public static void HandleCommand(string cmd, int keyCode, bool isDown)
        {
            if (cmd == "key_event")
            {
                INPUT[] inputs = new INPUT[1];
                inputs[0].type = INPUT_KEYBOARD;
                
                uint scanCode = MapVirtualKey((uint)keyCode, 0);

                inputs[0].U.ki.wVk = (ushort)keyCode; 
                inputs[0].U.ki.wScan = (ushort)scanCode;

                uint flags = 0;
                if (scanCode > 0) flags |= KEYEVENTF_SCANCODE;
                if (!isDown) flags |= KEYEVENTF_KEYUP;
                if (IsExtendedKey(keyCode)) flags |= KEYEVENTF_EXTENDEDKEY;

                inputs[0].U.ki.dwFlags = flags;

                SendInput(1, inputs, INPUT.Size);
            }
        }

        private static bool IsExtendedKey(int keyCode)
        {
            return (keyCode >= 33 && keyCode <= 46) || keyCode == 91 || keyCode == 111 || keyCode == 108;
        }

        public static string GetHtml()
        {
            return @"<!DOCTYPE html>
<html lang='ru'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'>
    <title>XrdKey Ultra</title>
    <style>
        :root { 
            --bg: #121212;
            --base: #252525;
            --base-dark: #1e1e1e;
            --shadow: #0a0a0a;
            --highlight: #353535;
            --accent: #00E676;
            --text: #e0e0e0;
            --text-dim: #888;
        }
        
        * { box-sizing: border-box; -webkit-tap-highlight-color: transparent; -webkit-touch-callout: none; user-select: none; }

        body { 
            margin: 0; padding: 0; background: var(--bg); color: var(--text);
            overflow: hidden; width: 100vw; height: 100dvh;
            font-family: 'Roboto', 'Segoe UI', sans-serif; touch-action: none;
        }
        
        #rotator {
            position: absolute; top: 50%; left: 50%;
            width: 100dvh; height: 100vw;
            transform: translate(-50%, -50%) rotate(90deg);
            display: flex; flex-direction: column; padding: 10px;
        }

        .bar { 
            display: flex; justify-content: space-between; height: 30px; margin-bottom: 5px;
            align-items: center; padding: 0 10px; font-weight: 500; letter-spacing: 1px;
        }
        .btn-x { 
            background: #333; color: #ff5555; text-decoration: none; padding: 4px 12px; 
            font-size: 11px; border-radius: 4px; border: 1px solid #444; 
        }

        .kb { flex: 1; display: flex; flex-direction: column; }
        .row { display: flex; flex: 1; width: 100%; }
        
        /* Логика контейнеров кнопок:
           .k - это обертка (занимает место в сетке).
           .cap - это сама визуальная клавиша.
        */
        .k {
            flex: 1; 
            display: flex; 
            padding: 3px; /* Зазор для обычных кнопок */
            position: relative;
        }

        .cap {
            flex: 1;
            background: linear-gradient(145deg, var(--base), var(--base-dark));
            border-radius: 5px;
            display: flex; align-items: center; justify-content: center;
            font-size: 14px; font-weight: 600;
            box-shadow: 
                0 4px 0 var(--shadow), 
                inset 0 1px 0 rgba(255,255,255,0.1);
            transition: transform 0.05s, box-shadow 0.05s, background 0.1s;
            color: var(--text);
            border: 1px solid rgba(0,0,0,0.2);
        }

        /* Текст внутри кнопок не ловит события */
        .cap span { pointer-events: none; }

        /* Состояние нажатия */
        .k.on .cap {
            transform: translateY(3px); /* Эффект вдавливания */
            box-shadow: 0 1px 0 var(--shadow) !important;
            background: var(--accent) !important;
            color: #121212 !important;
            border-color: transparent;
        }

        /* --- Стилизация групп кнопок --- */

        /* Системные и F-клавиши */
        .k.sys .cap { background: #1a1a1a; color: var(--text-dim); font-size: 11px; height: 70%; margin-top: auto;}
        
        /* Модификаторы (Ctrl, Shift) */
        .k.mod .cap { background: #202020; color: #aaa; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; }
        .k.mod.on .cap { background: var(--accent); color: #000; }

        /* Enter / Esc */
        .k.ent .cap { background: #1e3a29; border: 1px solid #2a4f38; }
        .k.esc .cap { background: #3a1e1e; color: #ff6b6b; }

        /* NUMPAD (Убираем зазоры) */
        .k.num {
            padding: 0; /* Убираем внешний отступ - кнопки слипаются */
        }
        .k.num .cap {
            border-radius: 0; /* Делаем квадратными */
            background: #181818;
            box-shadow: inset 0 0 0 1px #000; /* Внутренняя граница вместо тени */
            color: #ccc;
        }
        /* Скругляем углы у всего блока Numpad (опционально, для красоты угловых кнопок) */
        /* Здесь упрощено для производительности */

        /* Разделитель перед Numpad */
        .k.num-start {
            margin-left: 8px; /* Визуальный разрыв перед Numpad */
            position: relative;
        }
        .k.num-start::before {
            content: ''; position: absolute; left: -5px; top: 10%; bottom: 10%; 
            width: 2px; background: #222; border-radius: 2px;
        }

        /* Пробел */
        .k.spc { flex: 4.5; }

    </style>
</head>
<body>
    <div id='rotator'>
<div class='bar'>
    <div onclick=""window.location.href='/'"" class='btn-x' style=""cursor:pointer;"">
        BACK TO REMOTE
    </div>
    <span style='color:#666; font-size:10px;'>XRD REMOTE v2.0</span>
</div>
        
        <div class='kb' id='kb'></div>
    </div>

    <script>
        document.addEventListener('gesturestart', e => e.preventDefault());
        document.addEventListener('contextmenu', e => e.preventDefault());

        /* s: style class
           w: width (flex grow)
           m: mode (1 = toggle/modifier)
           ns: numpad start (флаг начала блока нампада)
        */
        const L = [
            [
                {t:'ESC',c:27,s:'esc'}, {t:'F1',c:112,s:'sys'}, {t:'F2',c:113,s:'sys'}, {t:'F3',c:114,s:'sys'}, {t:'F4',c:115,s:'sys'}, {t:'F5',c:116,s:'sys'}, {t:'F6',c:117,s:'sys'}, {t:'F7',c:118,s:'sys'}, {t:'F8',c:119,s:'sys'}, {t:'F9',c:120,s:'sys'}, {t:'F10',c:121,s:'sys'}, {t:'F11',c:122,s:'sys'}, {t:'F12',c:123,s:'sys'}, {t:'DEL',c:46,s:'esc'}, 
                {t:'NUM',c:144,s:'num',ns:true}, {t:'/',c:111,s:'num'}, {t:'*',c:106,s:'num'}, {t:'-',c:109,s:'num'}
            ],
            [
                {t:'~',c:192}, {t:'1',c:49}, {t:'2',c:50}, {t:'3',c:51}, {t:'4',c:52}, {t:'5',c:53}, {t:'6',c:54}, {t:'7',c:55}, {t:'8',c:56}, {t:'9',c:57}, {t:'0',c:48}, {t:'-',c:189}, {t:'=',c:187}, {t:'⌫',c:8,w:1.5,s:'mod'},
                {t:'7',c:103,s:'num',ns:true}, {t:'8',c:104,s:'num'}, {t:'9',c:105,s:'num'}, {t:'+',c:107,s:'num'}
            ],
            [
                {t:'TAB',c:9,w:1.2,s:'mod'}, {t:'Q',c:81}, {t:'W',c:87}, {t:'E',c:69}, {t:'R',c:82}, {t:'T',c:84}, {t:'Y',c:89}, {t:'U',c:85}, {t:'I',c:73}, {t:'O',c:79}, {t:'P',c:80}, {t:'[',c:219}, {t:']',c:221}, {t:'\\',c:220},
                {t:'4',c:100,s:'num',ns:true}, {t:'5',c:101,s:'num'}, {t:'6',c:102,s:'num'}, {t:'+',c:107,s:'num'} 
            ],
            [
                {t:'CAPS',c:20,w:1.4,s:'mod'}, {t:'A',c:65}, {t:'S',c:83}, {t:'D',c:68}, {t:'F',c:70}, {t:'G',c:71}, {t:'H',c:72}, {t:'J',c:74}, {t:'K',c:75}, {t:'L',c:76}, {t:';',c:186}, {t:'\'',c:222}, {t:'ENTER',c:13,w:1.8,s:'ent'},
                {t:'1',c:97,s:'num',ns:true}, {t:'2',c:98,s:'num'}, {t:'3',c:99,s:'num'}, {t:'ENT',c:13,s:'num'}
            ],
            [
                {t:'SHIFT',c:160,w:1.8,m:1,s:'mod'}, {t:'Z',c:90}, {t:'X',c:88}, {t:'C',c:67}, {t:'V',c:86}, {t:'B',c:66}, {t:'N',c:78}, {t:'M',c:77}, {t:',',c:188}, {t:'.',c:190}, {t:'/',c:191}, {t:'↑',c:38,s:'arr'}, {t:'SHIFT',c:161,s:'mod',m:1},
                {t:'0',c:96,w:2,s:'num',ns:true}, {t:'.',c:110,s:'num'}, {t:'ENT',c:13,s:'num'}
            ],
            [
                {t:'CTRL',c:162,w:1.2,m:1,s:'mod'}, {t:'WIN',c:91,s:'mod'}, {t:'ALT',c:164,m:1,s:'mod'}, {t:'',c:32,s:'spc'}, {t:'ALT',c:165,m:1,s:'mod'}, {t:'CTRL',c:163,m:1,s:'mod'}, {t:'←',c:37,s:'arr'}, {t:'↓',c:40,s:'arr'}, {t:'→',c:39,s:'arr'}, 
                {t:'0',c:96,w:2,s:'num',ns:true}, {t:'.',c:110,s:'num'}, {t:'ENT',c:13,s:'num'}
            ]
        ];

        const kb = document.getElementById('kb');
        const ip = localStorage.getItem('xrd_ip');

        L.forEach(row => {
            const r = document.createElement('div');
            r.className = 'row';
            row.forEach(k => {
                const wrapper = document.createElement('div');
                let className = 'k';
                if (k.s) className += ' ' + k.s;
                if (k.ns) className += ' num-start';
                wrapper.className = className;
                
                if(k.w) wrapper.style.flex = k.w;

                const cap = document.createElement('div');
                cap.className = 'cap';
                cap.innerHTML = '<span>' + k.t + '</span>';
                
                wrapper.appendChild(cap);
                
                // Логика нажатий
                const on = (e) => {
                    e.preventDefault();
                    e.stopPropagation(); 
                    if(k.m) { 
                        let a = wrapper.classList.toggle('on');
                        send(k.c, a);
                    } else {
                        if(wrapper.classList.contains('on')) return;
                        wrapper.classList.add('on');
                        send(k.c, true);
                    }
                };
                const off = (e) => {
                    e.preventDefault();
                    if(!k.m) {
                        wrapper.classList.remove('on');
                        send(k.c, false);
                    }
                };

                wrapper.addEventListener('touchstart', on, {passive:false});
                wrapper.addEventListener('touchend', off, {passive:false});
                
                wrapper.addEventListener('touchmove', e => {
                    let t = e.touches[0];
                    let el = document.elementFromPoint(t.clientX, t.clientY);
                    // Проверяем, находится ли палец все еще над ЭТИМ враппером или его детьми
                    if(!wrapper.contains(el) && !k.m) {
                        wrapper.classList.remove('on');
                        send(k.c, false);
                    } 
                }, {passive:false});

                r.appendChild(wrapper);
            });
            kb.appendChild(r);
        });

        function send(c, s) {
            if(!ip || !c) return;
            // Вибрация при нажатии (если поддерживается браузером)
            if(s && navigator.vibrate) navigator.vibrate(5);
            
            fetch(`http://${ip}/command`, {
                method:'POST', 
                body:JSON.stringify({cmd:'key_event', id:parseInt(c), vol:s?1:0})
            }).catch(()=>{});
        }
    </script>
</body>
</html>";
        }
    }
}
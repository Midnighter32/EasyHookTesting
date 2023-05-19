#include <windows.h>
#include <windowsx.h>
#include <d3d9.h>
#include <d3dx9core.h>
#include <chrono>
#include <ctime> 

#include <psapi.h>

#define SCREEN_WIDTH 800
#define SCREEN_HEIGHT 600

#pragma comment (lib, "d3d9.lib")
#pragma comment (lib, "d3dx9.lib")

LPDIRECT3D9 d3d;
LPDIRECT3DDEVICE9 d3ddev;
LPDIRECT3DVERTEXBUFFER9 v_buffer = NULL;
LPD3DXFONT m_font = NULL;

const char* get_base_address();
const char* get_device_address();
const char* get_current_time();
const char* get_custom_text();

void initD3D(HWND hWnd);
void render_frame(void);
void cleanD3D(void);
void init_graphics(void);

struct CUSTOMVERTEX { FLOAT X, Y, Z, RHW; DWORD COLOR; };
#define CUSTOMFVF (D3DFVF_XYZRHW | D3DFVF_DIFFUSE)

LRESULT CALLBACK WindowProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam);

int WINAPI WinMain(
    _In_ HINSTANCE hInstance,
    _In_opt_ HINSTANCE hPrevInstance,
    _In_ LPSTR lpCmdLine,
    _In_ int nCmdShow)
{
    auto pId = GetCurrentProcessId();
    char buffer[32];
    sprintf(buffer, "Dummy Window (%i)", pId);

    HWND hWnd;
    WNDCLASSEX wc;

    ZeroMemory(&wc, sizeof(WNDCLASSEX));

    wc.cbSize = sizeof(WNDCLASSEX);
    wc.style = CS_HREDRAW | CS_VREDRAW;
    wc.lpfnWndProc = WindowProc;
    wc.hInstance = hInstance;
    wc.hCursor = LoadCursor(NULL, IDC_ARROW);
    wc.hbrBackground = (HBRUSH)COLOR_WINDOW;
    wc.lpszClassName = "SDL_app";

    RegisterClassEx(&wc);

    hWnd = CreateWindowEx(NULL, "SDL_app", buffer, WS_OVERLAPPEDWINDOW,
        300, 300, 800, 600, NULL, NULL, hInstance, NULL);

    ShowWindow(hWnd, nCmdShow);

    initD3D(hWnd);

    MSG msg;

    while (TRUE)
    {
        while (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE))
        {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }

        if (msg.message == WM_QUIT)
            break;

        render_frame();
    }

    cleanD3D();

    return msg.wParam;
}

LRESULT CALLBACK WindowProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    switch (message)
    {
        case WM_DESTROY:
        {
            PostQuitMessage(0);
            return 0;
        } break;
    }

    return DefWindowProc(hWnd, message, wParam, lParam);
}

void initD3D(HWND hWnd)
{
    d3d = Direct3DCreate9(D3D_SDK_VERSION);

    D3DPRESENT_PARAMETERS d3dpp;

    ZeroMemory(&d3dpp, sizeof(d3dpp));
    d3dpp.Windowed = TRUE;
    d3dpp.SwapEffect = D3DSWAPEFFECT_DISCARD;
    d3dpp.hDeviceWindow = hWnd;

    d3d->CreateDevice(D3DADAPTER_DEFAULT,
        D3DDEVTYPE_HAL,
        hWnd,
        D3DCREATE_SOFTWARE_VERTEXPROCESSING,
        &d3dpp,
        &d3ddev);

    init_graphics();
}

void render_frame(void)
{
    d3ddev->Clear(0, NULL, D3DCLEAR_TARGET, D3DCOLOR_XRGB(0, 0, 0), 1.0f, 0);

    d3ddev->BeginScene();

        //d3ddev->SetFVF(CUSTOMFVF);

        //d3ddev->SetStreamSource(0, v_buffer, 0, sizeof(CUSTOMVERTEX));

        //d3ddev->DrawPrimitive(D3DPT_TRIANGLELIST, 0, 1);

        D3DCOLOR fontColor = D3DCOLOR_ARGB(255, 255, 0, 0);

        RECT rct{ 10, 10, 250, 30 };

        m_font->DrawText(NULL, get_current_time(), -1, &rct, 0, fontColor);

        rct.top = 40;
        rct.bottom = 60;

        m_font->DrawText(NULL, get_device_address(), -1, &rct, 0, fontColor);

        rct.top = 70;
        rct.bottom = 90;

        m_font->DrawText(NULL, get_base_address(), -1, &rct, 0, fontColor);

        rct.top = 100;
        rct.bottom = 120;

        m_font->DrawText(NULL, get_custom_text(), -1, &rct, 0, fontColor);

    d3ddev->EndScene();

    d3ddev->Present(NULL, NULL, NULL, NULL);
}

const char* get_current_time()
{
    auto now = std::chrono::system_clock::to_time_t(
        std::chrono::system_clock::now()
    );

    auto time = std::ctime(&now);

    return time;
}

const char* get_device_address()
{
    char buffer[32];
    sprintf(buffer, "D3DDevice: 0x%p", d3ddev);

    return buffer;
}

const char* get_custom_text()
{
    return "awesome text";
}

DWORD_PTR GetProcessBaseAddress(DWORD processID)
{
    DWORD_PTR   baseAddress = 0;
    HANDLE      processHandle = OpenProcess(PROCESS_ALL_ACCESS, FALSE, processID);
    HMODULE* moduleArray;
    LPBYTE      moduleArrayBytes;
    DWORD       bytesRequired;

    if (processHandle)
    {
        if (EnumProcessModules(processHandle, NULL, 0, &bytesRequired))
        {
            if (bytesRequired)
            {
                moduleArrayBytes = (LPBYTE)LocalAlloc(LPTR, bytesRequired);

                if (moduleArrayBytes)
                {
                    unsigned int moduleCount;

                    moduleCount = bytesRequired / sizeof(HMODULE);
                    moduleArray = (HMODULE*)moduleArrayBytes;

                    if (EnumProcessModules(processHandle, moduleArray, bytesRequired, &bytesRequired))
                    {
                        baseAddress = (DWORD_PTR)moduleArray[0];
                    }

                    LocalFree(moduleArrayBytes);
                }
            }
        }

        CloseHandle(processHandle);
    }

    return baseAddress;
}

const char* get_base_address()
{
    DWORD processID = GetCurrentProcessId();

    char buffer[33];
    sprintf(buffer, "Base Address: 0x%p", GetProcessBaseAddress(processID));

    return buffer;
}

void cleanD3D(void)
{
    v_buffer->Release();
    d3ddev->Release();
    d3d->Release();
}

void init_graphics(void)
{
    D3DXCreateFont(d3ddev, 17, 0, FW_BOLD, 0, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, ANTIALIASED_QUALITY, DEFAULT_PITCH | FF_DONTCARE, TEXT("Arial"), &m_font);

    CUSTOMVERTEX vertices[] =
    {
        { 400.0f, 62.5f, 0.5f, 1.0f, D3DCOLOR_XRGB(0, 0, 255), },
        { 650.0f, 500.0f, 0.5f, 1.0f, D3DCOLOR_XRGB(0, 255, 0), },
        { 150.0f, 500.0f, 0.5f, 1.0f, D3DCOLOR_XRGB(255, 0, 0), },
    };

    d3ddev->CreateVertexBuffer(3 * sizeof(CUSTOMVERTEX),
        0,
        CUSTOMFVF,
        D3DPOOL_MANAGED,
        &v_buffer,
        NULL);

    VOID* pVoid;

    v_buffer->Lock(0, 0, (void**)&pVoid, 0);
    memcpy(pVoid, vertices, sizeof(vertices));
    v_buffer->Unlock();
}
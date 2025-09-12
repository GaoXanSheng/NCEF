#pragma once

#include "Spout/SpoutDX.h"
#include <msclr/marshal_cppstd.h>

#using <mscorlib.dll>
#using <System.Runtime.dll>

using namespace System;
using namespace System::Runtime::InteropServices; 
using namespace msclr::interop;

namespace SpoutDXWrapper {

    public ref class SpoutSender
    {
    private:
        spoutDX* sender;
    public:
        SpoutSender() {
            sender = new spoutDX();
        }

        ~SpoutSender() {
            this->!SpoutSender();
        }

        !SpoutSender() {
            if(sender) {
                sender->ReleaseSender();
                sender->CloseDirectX11();
                delete sender;
                sender = nullptr;
            }
        }

        bool OpenDirectX() {
            return sender->OpenDirectX11();
        }
        bool OpenDirectX(IntPtr devicePtr) {
            ID3D11Device* pDevice = static_cast<ID3D11Device*>(devicePtr.ToPointer());
            return sender->OpenDirectX11(pDevice);
        }

        void SetSenderName(String^ name) {
            marshal_context context;
            const char* cname = context.marshal_as<const char*>(name);
            sender->SetSenderName(cname);
        }

        void SetSenderFormat(int format) {
            sender->SetSenderFormat(static_cast<DXGI_FORMAT>(format));
        }
        IntPtr GetID3D11Device() {
            return IntPtr((void*)sender->GetDX11Device());
        }
        bool SendTexture(IntPtr texturePtr) {
            ID3D11Texture2D* pTex = reinterpret_cast<ID3D11Texture2D*>(texturePtr.ToPointer());
            return sender->SendTexture(pTex);
        }

        void Release() {
            sender->ReleaseSender();
        }

        String^ GetName() {
            return gcnew String(sender->GetName());
        }

        unsigned int GetWidth() { return sender->GetWidth(); }
        unsigned int GetHeight() { return sender->GetHeight(); }
        double GetFps() { return sender->GetFps(); }
    };
}

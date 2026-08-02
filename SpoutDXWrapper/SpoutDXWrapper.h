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

    public ref class SpoutReceiver
    {
    private:
        spoutDX* receiver;
    public:
        SpoutReceiver() {
            receiver = new spoutDX();
        }

        ~SpoutReceiver() {
            this->!SpoutReceiver();
        }

        !SpoutReceiver() {
            if(receiver) {
                receiver->ReleaseReceiver();
                receiver->CloseDirectX11();
                delete receiver;
                receiver = nullptr;
            }
        }

        bool OpenDirectX() {
            return receiver->OpenDirectX11();
        }

        bool OpenDirectX(IntPtr devicePtr) {
            ID3D11Device* pDevice = static_cast<ID3D11Device*>(devicePtr.ToPointer());
            return receiver->OpenDirectX11(pDevice);
        }

        void SetReceiverName(String^ name) {
            marshal_context context;
            const char* cname = context.marshal_as<const char*>(name);
            receiver->SetReceiverName(cname);
        }
        bool SelectSender(){
           return receiver->SelectSender();
        }

        String^ GetSenderName() {
            const char* name = receiver->GetSenderName();
            return name ? gcnew String(name) : nullptr;
        }
        bool ReceiveToExistingTexture(IntPtr destTexturePtr) {
            if (destTexturePtr == IntPtr::Zero) return false;
            ID3D11Texture2D* pDestTex = reinterpret_cast<ID3D11Texture2D*>(destTexturePtr.ToPointer());
            return receiver->ReceiveTexture(&pDestTex);
        }
        IntPtr ReceiveTexturePtr() {
            if (!receiver->ReceiveTexture()) {
                return IntPtr::Zero;
            }
            ID3D11Texture2D* tex = receiver->GetSenderTexture();
            if (!tex) {
                return IntPtr::Zero;
            }
            tex->AddRef(); 
            return IntPtr((void*)tex);
        }
        unsigned int GetWidth() { return receiver->GetSenderWidth(); }
        unsigned int GetHeight() { return receiver->GetSenderHeight(); }
        double GetFps() { return receiver->GetSenderFps(); }

        bool IsConnected() {
            return receiver->IsConnected();
        }
    };
}

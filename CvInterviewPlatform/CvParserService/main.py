import os
import tempfile
import logging
from fastapi import FastAPI, UploadFile, File, HTTPException
from docling.datamodel.base_models import InputFormat
from docling.datamodel.accelerator_options import AcceleratorOptions
from docling.datamodel.pipeline_options import PdfPipelineOptions
from docling.document_converter import DocumentConverter, PdfFormatOption

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("cv-parser-service")

app = FastAPI(title="Docling CV Parser Service")

ALLOWED_EXTENSIONS = {".pdf", ".docx", ".txt", ".md", ".png", ".jpg", ".jpeg"}
MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024

# Initialize Docling DocumentConverter at startup
logger.info("Initializing IBM Docling DocumentConverter...")
try:
    # do_table_structure=False: tableformer modellerini indirmeyi atlıyoruz (~40MB).
    # do_ocr=True bırakıldı: Docling, born-digital (dijital metin katmanlı)
    # sayfalarda OCR modelini zaten bitmap_area_threshold sezgiselliğiyle atlıyor —
    # yerel ölçümde do_ocr=True/False arasında fark çıkmadı (~5.5sn, ikisinde de),
    # bu yüzden do_ocr=False'u taranmış/görüntü PDF desteğini kaybetme riskine
    # değmeyeceği için kapatmadık. num_threads sunucu çekirdek sayısına eşitlendi.
    pipeline_options = PdfPipelineOptions(
        do_table_structure=False,
        accelerator_options=AcceleratorOptions(num_threads=os.cpu_count() or 4),
    )
    converter = DocumentConverter(
        format_options={
            InputFormat.PDF: PdfFormatOption(pipeline_options=pipeline_options)
        }
    )
    logger.info("IBM Docling DocumentConverter initialized successfully (table structure parsing disabled).")
except Exception as e:
    logger.error(f"Failed to initialize IBM Docling DocumentConverter: {e}")
    converter = None

@app.get("/health")
def health_check():
    return {
        "status": "ok",
        "converter_ready": converter is not None,
        "supported_formats": sorted(ALLOWED_EXTENSIONS),
    }

@app.post("/parse")
async def parse_document(file: UploadFile = File(...)):
    filename = (file.filename or "").strip('"').strip("'").lower()
    ext = os.path.splitext(filename)[1]
    if ext not in ALLOWED_EXTENSIONS:
        logger.error(f"Validation failed: unsupported extension '{ext}' for filename '{file.filename}'")
        raise HTTPException(
            status_code=400,
            detail=f"Unsupported file format '{ext}'. Allowed formats: {', '.join(sorted(ALLOWED_EXTENSIONS))}"
        )

    content = await file.read()
    if len(content) > MAX_FILE_SIZE_BYTES:
        logger.error(f"Validation failed: file '{file.filename}' exceeds size limit ({len(content)} bytes)")
        raise HTTPException(status_code=400, detail="File exceeds the 10 MB size limit.")

    # .txt zaten düz metin — Docling'in dosya format dönüştürme hattına hiç
    # sokmadan doğrudan okuyoruz (Docling'in native formatlarından biri değil).
    if ext == ".txt":
        try:
            return {"markdown": content.decode("utf-8", errors="replace")}
        except Exception as e:
            logger.error(f"Error reading text file: {e}")
            raise HTTPException(status_code=500, detail=f"Could not read text file: {str(e)}")

    if converter is None:
        raise HTTPException(status_code=500, detail="IBM Docling converter is not initialized.")

    tmp_path = None
    try:
        # Geçici dosya gerçek uzantıyla oluşturuluyor — Docling formatı bu
        # uzantıdan algılıyor, sabit ".pdf" DOCX/PNG gibi dosyaları PDF sanıp
        # parse hatası verdiriyordu.
        with tempfile.NamedTemporaryFile(delete=False, suffix=ext) as tmp:
            tmp.write(content)
            tmp_path = tmp.name

        logger.info(f"Received file: {file.filename}, saved to temporary path: {tmp_path}")

        # Convert using Docling
        result = converter.convert(tmp_path)
        markdown_text = result.document.export_to_markdown()

        logger.info(f"Successfully parsed {file.filename}")
        return {"markdown": markdown_text}

    except Exception as e:
        logger.error(f"Error parsing document: {e}")
        raise HTTPException(status_code=500, detail=f"Parsing failed: {str(e)}")

    finally:
        if tmp_path and os.path.exists(tmp_path):
            try:
                os.unlink(tmp_path)
            except Exception as ex:
                logger.error(f"Failed to delete temp file {tmp_path}: {ex}")

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="127.0.0.1", port=8000)

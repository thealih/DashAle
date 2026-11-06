import re


def product_url(source_id: str) -> str:
    """Canonical public URL; API identifiers stay numeric."""
    value = str(source_id).removeprefix('snp-')
    if not re.fullmatch(r'\d{3,30}', value):
        raise ValueError('Invalid Snapp! Shop product id')
    return f'https://snappshop.ir/product/snp-{value}'
